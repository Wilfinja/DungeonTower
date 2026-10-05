using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    public static class PathFinder
    {
        // Cheapest walkable route (diagonals allowed, no corner cutting —
        // see GridSteps), excluding the origin and including the
        // destination.
        public static List<GridPosition> FindPath(
            GridPosition origin, GridPosition destination, IWalkableMap map,
            IReadOnlyCollection<GridPosition> occupiedTiles = null)
        {
            var path = new List<GridPosition>();
            if (origin.Equals(destination)) return path;

            var blocked = occupiedTiles != null ? new HashSet<GridPosition>(occupiedTiles) : null;
            var cameFrom = new Dictionary<GridPosition, GridPosition>();
            GridSteps.CostMap(origin, map, blocked, GridSteps.NoBudget, cameFrom, destination);

            if (!cameFrom.ContainsKey(destination))
            {
                path.Add(destination);   // no route found: fall back to a single hop
                return path;
            }

            for (var step = destination; !step.Equals(origin); step = cameFrom[step])
                path.Add(step);
            path.Reverse();
            return path;
        }
    }
}
