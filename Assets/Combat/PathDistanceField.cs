using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Cheapest walkable-path cost from one tile to every other tile
    /// reachable from it, respecting walls but not occupancy (occupancy
    /// is transient turn to turn; walls aren't). Used to rank a unit's
    /// reachable-this-turn tiles by real path progress toward a target
    /// instead of straight-line distance — which is what let an enemy
    /// walk into a dead-end corner that merely looked close.
    ///
    /// Values are in half-steps (straight 2, diagonal 3 — see GridSteps),
    /// which is fine for ranking; divide by 2 for "tiles".
    /// </summary>
    public static class PathDistanceField
    {
        public static Dictionary<GridPosition, int> BuildFrom(GridPosition origin, IWalkableMap map)
            => GridSteps.CostMap(origin, map, null, GridSteps.NoBudget);
    }
}
