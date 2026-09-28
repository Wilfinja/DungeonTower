using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One unit's memory of where it (or an ally in its alerted pack —
    /// see BattleController.RecordSighting) last actually SAW each
    /// hostile. Nothing here decides who to chase or when a sighting
    /// counts as "current" vs "stale" — that's
    /// BattleController.PickKnownTarget's job, using MaxAgeRounds as the
    /// cutoff. This is pure storage: record a sighting, read it back
    /// while it's still fresh enough, forget it.
    ///
    /// Lives on CombatUnit (see CombatUnit.Memory) the same way
    /// StatusEffectTracker does — per-unit runtime state, always
    /// present, harmless and simply unused on a player-controlled unit.
    /// </summary>
    public sealed class TargetMemory
    {
        private readonly Dictionary<CombatUnit, (GridPosition Position, int Round)> _sightings
            = new Dictionary<CombatUnit, (GridPosition Position, int Round)>();

        // Overwrites any earlier sighting of this target — a memory only
        // ever holds the MOST RECENT known position, never a history.
        public void Record(CombatUnit target, GridPosition position, int round)
            => _sightings[target] = (position, round);

        // Null if this target has never been seen, or its last sighting
        // is older than maxAgeRounds relative to currentRound — the
        // trail's gone cold, as far as the caller should be concerned.
        public GridPosition? Get(CombatUnit target, int currentRound, int maxAgeRounds)
        {
            if (_sightings.TryGetValue(target, out var entry) && currentRound - entry.Round <= maxAgeRounds)
                return entry.Position;
            return null;
        }

        public void Forget(CombatUnit target) => _sightings.Remove(target);

        public void Clear() => _sightings.Clear();
    }
}
