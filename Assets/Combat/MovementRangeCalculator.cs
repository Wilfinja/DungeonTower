using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Every tile a unit can reach this turn within its move range,
    /// walking eight ways (diagonals cost 1.5, no corner cutting — see
    /// GridSteps), respecting walls/bounds via IWalkableMap and,
    /// optionally, tiles occupied by other units — pass the current
    /// positions of every other living unit so the mover can't path onto,
    /// through, or diagonally around them.
    /// </summary>
    public static class MovementRangeCalculator
    {
        public static HashSet<GridPosition> GetReachableTiles(
            GridPosition origin, int moveRange, IWalkableMap map,
            IReadOnlyCollection<GridPosition> occupiedTiles = null)
        {
            var blocked = occupiedTiles != null ? new HashSet<GridPosition>(occupiedTiles) : null;
            var costs = GridSteps.CostMap(origin, map, blocked, GridSteps.Budget(moveRange));

            var reachable = new HashSet<GridPosition>(costs.Keys);
            reachable.Remove(origin);
            return reachable;
        }

        // Same, but the budget is given directly in half-steps (what a hero
        // has left in their action pool) rather than as whole tiles.
        public static HashSet<GridPosition> GetReachableTilesWithBudget(
            GridPosition origin, int budgetHalfSteps, IWalkableMap map,
            IReadOnlyCollection<GridPosition> occupiedTiles = null)
        {
            var blocked = occupiedTiles != null ? new HashSet<GridPosition>(occupiedTiles) : null;
            var costs = GridSteps.CostMap(origin, map, blocked, budgetHalfSteps);

            var reachable = new HashSet<GridPosition>(costs.Keys);
            reachable.Remove(origin);
            return reachable;
        }
    }
}
