using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Shortest walkable-path distance from one tile to every other tile
    /// reachable from it, respecting walls but not occupancy (occupancy
    /// is transient turn to turn; walls aren't). Used to rank a unit's
    /// reachable-this-turn tiles by real path progress toward a target
    /// instead of straight-line Manhattan distance — which is what let an
    /// enemy walk into a dead-end corner that merely looked close.
    /// </summary>
    public static class PathDistanceField
    {
        public static Dictionary<GridPosition, int> BuildFrom(GridPosition origin, IWalkableMap map)
        {
            var distances = new Dictionary<GridPosition, int> { [origin] = 0 };
            var frontier = new Queue<GridPosition>();
            frontier.Enqueue(origin);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int d = distances[current];

                foreach (var neighbor in current.Neighbors())
                {
                    if (distances.ContainsKey(neighbor)) continue;
                    if (!map.InBounds(neighbor) || !map.IsWalkable(neighbor)) continue;
                    distances[neighbor] = d + 1;
                    frontier.Enqueue(neighbor);
                }
            }

            return distances;
        }
    }
}
