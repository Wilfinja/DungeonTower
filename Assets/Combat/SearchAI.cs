using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Picks a next step for an alerted unit that's lost its target but
    /// hasn't fully given up yet — a random reachable tile within
    /// `radius` of wherever it (or its pack) last actually saw someone,
    /// rather than beelining straight for that exact tile the way a
    /// live chase does. Mirrors RoamAI's shape exactly (reachable tiles
    /// filtered to a zone, one picked at random) but bounded by
    /// Manhattan distance from an arbitrary anchor point instead of a
    /// fixed RoomZone set, since a lost trail can be anywhere on the map
    /// — not just within the unit's original room/corridor patch.
    /// </summary>
    public static class SearchAI
    {
        public static GridPosition? ChooseStep(
            CombatUnit unit, GridPosition anchor, int radius, IWalkableMap map,
            IReadOnlyCollection<GridPosition> occupiedTiles, Random rng)
        {
            var reachable = MovementRangeCalculator.GetReachableTiles(unit.Position, unit.Stats.MoveRange, map, occupiedTiles);
            var candidates = reachable.Where(pos => pos.ManhattanDistance(anchor) <= radius).ToList();
            if (candidates.Count == 0) return null;

            return candidates[rng.Next(candidates.Count)];
        }
    }
}
