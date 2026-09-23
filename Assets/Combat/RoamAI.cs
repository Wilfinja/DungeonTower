using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Picks a next step for an idle, un-alerted unit with a roam zone —
    /// a random tile within that zone that's actually reachable this turn,
    /// respecting walls and occupied tiles via the same movement-range
    /// logic combat uses everywhere else. A unit with no roam zone simply
    /// doesn't roam: ChooseStep returns null and it stays put, exactly the
    /// behavior idle enemies had before roaming existed.
    /// </summary>
    public static class RoamAI
    {
        public static GridPosition? ChooseStep(
            CombatUnit unit, IWalkableMap map, IReadOnlyCollection<GridPosition> occupiedTiles, Random rng)
        {
            if (unit.RoamZone == null || unit.RoamZone.Count == 0) return null;

            var reachable = MovementRangeCalculator.GetReachableTiles(unit.Position, unit.Stats.MoveRange, map, occupiedTiles);
            var candidates = reachable.Where(pos => unit.RoamZone.Contains(pos)).ToList();
            if (candidates.Count == 0) return null;

            return candidates[rng.Next(candidates.Count)];
        }
    }
}
