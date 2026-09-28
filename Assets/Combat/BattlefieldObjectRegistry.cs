using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Every currently-active BattlefieldObject. Pure bookkeeping and
    /// queries — no RNG, no Debug.Log, no view spawning — so
    /// BattleController stays the one place that decides what a trigger
    /// actually DOES (apply statuses, log it, refresh a view). This
    /// mirrors StatusEffectTracker's split: the tracker owns the
    /// lifecycle, IStatusBehavior owns the effect; here the registry
    /// owns the lifecycle and BattleController owns the effect.
    /// </summary>
    public sealed class BattlefieldObjectRegistry
    {
        private readonly List<BattlefieldObject> _objects = new List<BattlefieldObject>();

        public IReadOnlyList<BattlefieldObject> Active => _objects;

        public void Add(BattlefieldObject obj) => _objects.Add(obj);
        public void Remove(BattlefieldObject obj) => _objects.Remove(obj);

        public bool BlocksMovementAt(GridPosition pos)
            => _objects.Any(o => o.BlocksMovement && Occupies(o, pos));

        public bool BlocksSightAt(GridPosition pos)
            => _objects.Any(o => o.BlocksLineOfSight && Occupies(o, pos));

        // Only objects that would actually show a marker (Trap-type
        // hidden ones excluded) — for whatever draws battlefield views.
        public IEnumerable<BattlefieldObject> Visible => _objects.Where(o => !o.IsHidden);

        public IEnumerable<BattlefieldObject> At(GridPosition pos)
            => _objects.Where(o => Occupies(o, pos));

        // Every OnEntry object at `pos` that isn't skipping `unit`'s own
        // faction, removing any that are ConsumedAfterTrigger. Caller
        // (BattleController) is responsible for actually applying each
        // returned object's Statuses.
        public List<BattlefieldObject> TriggerOnEntry(CombatUnit unit, GridPosition pos)
        {
            var triggered = new List<BattlefieldObject>();
            foreach (var obj in _objects
                .Where(o => o.TriggerMode == SummonTriggerMode.OnEntry && Occupies(o, pos))
                .ToList())
            {
                if (obj.IgnoreOwnerFaction && obj.OwnerFaction == unit.Faction) continue;
                triggered.Add(obj);
                if (obj.ConsumedAfterTrigger) _objects.Remove(obj);
            }
            return triggered;
        }

        // Every unit that qualifies for an OnRoundTick object's aura
        // right now: alive, on the correct side of AffectsAllies
        // relative to the object's owner, and within AuraRadius of at
        // least one of its footprint tiles (footprint tiles themselves
        // always count, at radius 0).
        public List<CombatUnit> UnitsInAuraRange(BattlefieldObject obj, IEnumerable<CombatUnit> allUnits)
            => allUnits.Where(u => u.IsAlive
                    && (obj.AffectsAllies ? u.Faction == obj.OwnerFaction : u.Faction != obj.OwnerFaction)
                    && obj.Tiles.Any(t => u.Position.ManhattanDistance(t) <= obj.AuraRadius))
                .ToList();

        // Removes every object owned by `unit` that's tied to it
        // (EndsIfOwnerMoves) — call this from the same place Bleed/
        // Momentum's OnMoved hook fires, and again on death.
        public List<BattlefieldObject> RemoveOwnedByMovement(CombatUnit unit)
        {
            var doomed = _objects.Where(o => o.OwnerUnit == unit).ToList();
            foreach (var obj in doomed) _objects.Remove(obj);
            return doomed;
        }

        // Ticks every object's duration down by one round; returns the
        // ones that expired (already removed) so the caller can log/
        // clean up views. Does NOT apply OnRoundTick effects — that
        // needs live CombatUnit access and RNG, so it stays in
        // BattleController (see TickBattlefieldObjects).
        public List<BattlefieldObject> TickDuration()
        {
            var expired = new List<BattlefieldObject>();
            foreach (var obj in _objects.ToList())
            {
                if (obj.RemainingDuration == int.MaxValue) continue;
                obj.RemainingDuration--;
                if (obj.RemainingDuration <= 0)
                {
                    _objects.Remove(obj);
                    expired.Add(obj);
                }
            }
            return expired;
        }

        private static bool Occupies(BattlefieldObject obj, GridPosition pos)
            => obj.Tiles != null && obj.Tiles.Any(t => t.Equals(pos));
    }
}
