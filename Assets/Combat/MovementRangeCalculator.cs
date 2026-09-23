using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Flood-fills outward from a unit's position up to its move range,
    /// respecting walls/bounds via IWalkableMap and, optionally, tiles
    /// already occupied by other units — pass the current positions of
    /// every other living unit so the mover can't path onto or through them.
    /// </summary>
    public static class MovementRangeCalculator
    {
        public static HashSet<GridPosition> GetReachableTiles(
            GridPosition origin, int moveRange, IWalkableMap map,
            IReadOnlyCollection<GridPosition> occupiedTiles = null)
        {
            var occupied = occupiedTiles ?? Array.Empty<GridPosition>();
            var costSoFarByTile = new Dictionary<GridPosition, int> { [origin] = 0 };
            var frontier = new Queue<GridPosition>();
            frontier.Enqueue(origin);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                int costSoFar = costSoFarByTile[current];
                if (costSoFar >= moveRange) continue;

                foreach (var neighbor in current.Neighbors())
                {
                    if (costSoFarByTile.ContainsKey(neighbor)) continue;
                    if (!map.InBounds(neighbor) || !map.IsWalkable(neighbor)) continue;
                    if (Contains(occupied, neighbor)) continue;

                    costSoFarByTile[neighbor] = costSoFar + 1;
                    frontier.Enqueue(neighbor);
                }
            }

            var reachable = new HashSet<GridPosition>(costSoFarByTile.Keys);
            reachable.Remove(origin);
            return reachable;
        }

        private static bool Contains(IReadOnlyCollection<GridPosition> tiles, GridPosition position)
        {
            foreach (var tile in tiles)
                if (tile.Equals(position))
                    return true;
            return false;
        }
    }
}
