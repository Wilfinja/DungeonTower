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
        /// Tries to apply a status to the owner with ONE roll:
        ///   attempt chance = the application's Chance + the source's Potency (max 100)
        ///   final chance   = attempt chance - the owner's StatusResist
        ///                    (Harmful statuses only, never self-applied; never below
        ///                    MinStatusChance, or the attempt chance if that's lower)
        /// A roll above the attempt chance is ProcFailed; between the final and
        /// attempt chance it's Resisted. A successful application is also scaled
        /// by the source's Potency: stacks and magnitude (Burn %, Ward pool, Mark %,
        /// Regeneration stacks...) grow by that percent. Pass a null rng to skip the
        /// roll AND the potency scaling and apply exactly as authored (tests,
        /// scripted effects, the debug inspector).
        /// </summary>
        public StatusApplyResult Apply(StatusApplication app, CombatUnit source, Random rng)
        {
            var rule = StatusRules.Get(app.Id);

            float potency = rng != null && source != null ? source.Stats.Potency : 0f;
            float potencyScale = 1f + potency / 100f;

            if (rng != null)
            {
                bool resistable = rule.Polarity == StatusPolarity.Harmful && source != _owner;
                float resist = resistable ? _owner.Stats.StatusResist : 0f;

                float attemptChance = Math.Min(100f, app.EffectiveChance + potency);
                float floor = Math.Min(attemptChance, MinStatusChance);
                float finalChance = Math.Max(floor, attemptChance - resist);

                double roll = rng.NextDouble() * 100.0;
                if (roll >= attemptChance)
                    return new StatusApplyResult(StatusApplyOutcome.ProcFailed, null);
                if (roll >= finalChance)
                    return new StatusApplyResult(StatusApplyOutcome.Resisted, null);
            }

            int stacks = CapStacks(rule, ScaleCount(app.EffectiveStacks, potencyScale));
            float magnitude = app.Magnitude * potencyScale;
            int duration = rule.UsesDuration ? app.EffectiveDuration(rule) : 0;

            StatusApplyOutcome outcome;
            if (!_active.TryGetValue(app.Id, out var instance))
            {
                instance = new StatusEffectInstance(app.Id, stacks, duration, magnitude, app.StatBonus, source);
                _active[app.Id] = instance;
                outcome = StatusApplyOutcome.Applied;
            }
            else
            {
                bool durationHandled = false;
                switch (rule.Reapply)
                {
                    case ReapplyMode.AddStacks:
                        instance.Stacks = CapStacks(rule, instance.Stacks + stacks);
                        instance.Magnitude = Math.Max(instance.Magnitude, magnitude);
                        outcome = StatusApplyOutcome.Stacked;
                        break;

                    case ReapplyMode.KeepStronger:
                        if (magnitude > instance.Magnitude) instance.Magnitude = magnitude;
                        outcome = StatusApplyOutcome.Refreshed;
                        break;

                    case ReapplyMode.Accelerate:
                        instance.Magnitude += magnitude;
                        instance.Duration = Math.Max(1, instance.Duration + rule.ReapplyDurationShift);
                        durationHandled = true;
                        outcome = StatusApplyOutcome.Stacked;
                        break;

                    default: // RefreshDuration
                        outcome = StatusApplyOutcome.Refreshed;
                        break;
                }

                if (!durationHandled) instance.Duration = Math.Max(instance.Duration, duration);
                if (source != null) instance.Source = source;
            }

            if (IsOwnersTurn && rule.UsesDuration && !rule.TicksDurationAtTurnStart)
                instance.SkipNextDurationTick = true;

            RefreshBonuses();
            return new StatusApplyResult(outcome, instance);
        }

        // A status can never be made completely un-landable by resist alone.
        public const float MinStatusChance = 5f;

        private static int ScaleCount(int count, float scale)
            => Math.Max(count, (int)Math.Round(count * scale, MidpointRounding.AwayFromZero));

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

        // Targeted cleanse: empty/null = every Harmful status (same as
        // Cleanse()); otherwise exactly the listed ids, any polarity.
        public int Cleanse(IReadOnlyList<StatusEffectId> only)
        {
            if (only == null || only.Count == 0) return Cleanse();
            return RemoveWhere(i => only.Contains(i.Id));
        }

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
                if (!_owner.IsAlive) break;   // an earlier tick (or a Doom detonation) already killed the owner

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
        /// Call right after the owner moves, on every move path. Runs each
        /// status's OnMoved (Momentum arms, later Bleed hurts). Check
        /// OwnerDied and run death handling if set.
        /// </summary>
        public StatusTickResult OnMoved()
        {
            var result = new StatusTickResult();

            foreach (var instance in Snapshot())
            {
                StatusBehaviors.Get(instance.Id)?.OnMoved(_owner, instance, result);
                if (!_owner.IsAlive) break;
            }

            result.OwnerDied = !_owner.IsAlive;
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
