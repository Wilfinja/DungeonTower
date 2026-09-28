using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// The set of statuses currently active on one CombatUnit — the
    /// status-side counterpart to CombatUnit's cooldown dictionary, but
    /// with a richer lifecycle. Owns application (chance roll, resist
    /// roll, stacking), ticking (turn start/end), removal (expiry,
    /// Cleanse), and keeping UnitStats' temporary stat bonus in sync
    /// with whatever's active.
    ///
    /// Timing conventions (see StatusRule for the full picture):
    ///  - Turn START: behaviors tick (DoT/HoT), then stacks/magnitude
    ///    decay, then turn-start-duration statuses (Doom) count down.
    ///  - Turn END: ordinary duration statuses count down. A status
    ///    applied to a unit during its OWN turn skips that turn's
    ///    countdown, so "2 turns" always means two turns the owner
    ///    actually gets to benefit from (or suffer through).
    /// </summary>
    public sealed class StatusEffectTracker
    {
        private readonly CombatUnit _owner;
        private readonly Dictionary<StatusEffectId, StatusEffectInstance> _active
            = new Dictionary<StatusEffectId, StatusEffectInstance>();

        // True between OnTurnStart and OnTurnEnd for the owner.
        public bool IsOwnersTurn { get; private set; }

        public StatusEffectTracker(CombatUnit owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        // --- Queries ---

        public IReadOnlyList<StatusEffectInstance> Active => Snapshot();

        public bool Has(StatusEffectId id) => _active.ContainsKey(id);

        public StatusEffectInstance Get(StatusEffectId id)
            => _active.TryGetValue(id, out var instance) ? instance : null;

        public int GetStacks(StatusEffectId id) => _active.TryGetValue(id, out var i) ? i.Stacks : 0;

        // Stun: the unit's whole turn is skipped.
        public bool CanAct => !Has(StatusEffectId.Stun);

        // Root: can still act (attack, use items), just can't move.
        // A stunned unit can't move either.
        public bool CanMove => CanAct && !Has(StatusEffectId.Root);

        // --- Application ---

        /// <summary>
        /// Tries to apply a status to the owner. Two rolls happen first,
        /// in order: the application's own Chance (on-hit procs), then —
        /// for Harmful statuses only, and never when the owner applies
        /// it to itself — the owner's StatusResist. Pass a null rng to
        /// skip both rolls and apply deterministically (tests, scripted
        /// effects).
        /// </summary>
        public StatusApplyResult Apply(StatusApplication app, CombatUnit source, Random rng)
        {
            var rule = StatusRules.Get(app.Id);

            if (rng != null && app.EffectiveChance < 100f
                && rng.NextDouble() * 100.0 >= app.EffectiveChance)
                return new StatusApplyResult(StatusApplyOutcome.ProcFailed, null);

            if (rng != null && rule.Polarity == StatusPolarity.Harmful && source != _owner
                && rng.NextDouble() * 100.0 < _owner.Stats.StatusResist)
                return new StatusApplyResult(StatusApplyOutcome.Resisted, null);

            int stacks = CapStacks(rule, app.EffectiveStacks);
            int duration = rule.UsesDuration ? app.EffectiveDuration(rule) : 0;

            StatusApplyOutcome outcome;
            if (!_active.TryGetValue(app.Id, out var instance))
            {
                instance = new StatusEffectInstance(app.Id, stacks, duration, app.Magnitude, app.StatBonus, source);
                _active[app.Id] = instance;
                outcome = StatusApplyOutcome.Applied;
            }
            else
            {
                switch (rule.Reapply)
                {
                    case ReapplyMode.AddStacks:
                        instance.Stacks = CapStacks(rule, instance.Stacks + stacks);
                        instance.Magnitude = Math.Max(instance.Magnitude, app.Magnitude);
                        outcome = StatusApplyOutcome.Stacked;
                        break;

                    case ReapplyMode.KeepStronger:
                        if (app.Magnitude > instance.Magnitude) instance.Magnitude = app.Magnitude;
                        outcome = StatusApplyOutcome.Refreshed;
                        break;

                    default: // RefreshDuration
                        outcome = StatusApplyOutcome.Refreshed;
                        break;
                }

                instance.Duration = Math.Max(instance.Duration, duration);
                if (source != null) instance.Source = source;
            }

            if (IsOwnersTurn && rule.UsesDuration && !rule.TicksDurationAtTurnStart)
                instance.SkipNextDurationTick = true;

            RefreshBonuses();
            return new StatusApplyResult(outcome, instance);
        }

        private static int CapStacks(StatusRule rule, int stacks)
            => rule.MaxStacks > 0 ? Math.Min(stacks, rule.MaxStacks) : stacks;

        // --- Removal ---

        public bool Remove(StatusEffectId id)
        {
            bool removed = _active.Remove(id);
            if (removed) RefreshBonuses();
            return removed;
        }

        public int RemoveWhere(Func<StatusEffectInstance, bool> predicate)
        {
            var doomed = Snapshot().Where(predicate).ToList();
            foreach (var instance in doomed) _active.Remove(instance.Id);
            if (doomed.Count > 0) RefreshBonuses();
            return doomed.Count;
        }

        // Cleanse: strips every Harmful status, leaves Beneficial ones.
        public int Cleanse()
            => RemoveWhere(i => StatusRules.Get(i.Id).Polarity == StatusPolarity.Harmful);

        public void Clear()
        {
            _active.Clear();
            RefreshBonuses();
        }

        // --- Ticking ---

        /// <summary>
        /// Call once at the start of the owner's turn, alongside
        /// TickCooldowns. Check the result: if OwnerDied, run death
        /// handling and end the turn; if SkipTurn, end the turn.
        /// </summary>
        public StatusTickResult OnTurnStart()
        {
            IsOwnersTurn = true;
            var result = new StatusTickResult();

            foreach (var instance in Snapshot())
            {
                var rule = StatusRules.Get(instance.Id);
                var behavior = StatusBehaviors.Get(instance.Id);

                if (behavior != null) behavior.OnTurnStart(_owner, instance, result);
                if (!_owner.IsAlive) break;

                if (rule.StackDecayPerTurn > 0) instance.Stacks -= rule.StackDecayPerTurn;
                if (rule.MagnitudeDecayPerTurn > 0f) instance.Magnitude -= rule.MagnitudeDecayPerTurn;
                if (rule.UsesDuration && rule.TicksDurationAtTurnStart) instance.Duration--;

                if (IsExpired(instance, rule)) Expire(instance, behavior, result);
            }

            RefreshBonuses();
            result.OwnerDied = !_owner.IsAlive;
            result.SkipTurn = _owner.IsAlive && !CanAct;
            return result;
        }

        /// <summary>
        /// Call once when the owner's turn ends, before advancing the
        /// turn order — on every path that ends a turn, including a
        /// skipped (stunned) one.
        /// </summary>
        public StatusTickResult OnTurnEnd()
        {
            var result = new StatusTickResult();

            foreach (var instance in Snapshot())
            {
                var rule = StatusRules.Get(instance.Id);
                if (!rule.UsesDuration || rule.TicksDurationAtTurnStart) continue;

                if (instance.SkipNextDurationTick)
                {
                    instance.SkipNextDurationTick = false;
                    continue;
                }

                instance.Duration--;
                if (instance.Duration <= 0)
                    Expire(instance, StatusBehaviors.Get(instance.Id), result);
            }

            IsOwnersTurn = false;
            RefreshBonuses();
            result.OwnerDied = !_owner.IsAlive;
            return result;
        }

        // --- Internals ---

        private static bool IsExpired(StatusEffectInstance instance, StatusRule rule)
            => (rule.UsesDuration && instance.Duration <= 0)
            || (rule.StackDecayPerTurn > 0 && instance.Stacks <= 0)
            || (rule.MagnitudeDecayPerTurn > 0f && instance.Magnitude <= 0f);

        private void Expire(StatusEffectInstance instance, IStatusBehavior behavior, StatusTickResult result)
        {
            if (behavior != null) behavior.OnExpired(_owner, instance, result);
            _active.Remove(instance.Id);
            result.Expired.Add(instance);
        }

        // Deterministic order (by id) so ticks resolve the same way every
        // run — Dictionary enumeration order isn't guaranteed.
        private List<StatusEffectInstance> Snapshot()
            => _active.Values.OrderBy(i => (int)i.Id).ToList();

        // Recomputes the sum of every active status's stat modifier and
        // pushes it into UnitStats; then re-clamps HP/MP in case a
        // modifier that was inflating Max just expired.
        private void RefreshBonuses()
        {
            var total = default(DerivedStatBonus);
            foreach (var instance in _active.Values)
                total += instance.EffectiveBonus;

            _owner.Stats.SetTemporaryBonus(total);
            _owner.ClampResources();
        }
    }
}
