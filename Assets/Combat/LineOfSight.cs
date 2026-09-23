using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Grid line-of-sight between two tiles, walked with Bresenham's line
    /// algorithm rather than a physics raycast — keeps this pure C# like
    /// the rest of Combat, with no dependency on Unity's physics engine or
    /// any Editor-side collider/layer setup. Blocked by any unwalkable
    /// tile strictly between the two endpoints; the endpoints themselves
    /// never block each other, so adjacent (melee-range) tiles always
    /// have clear line of sight by definition — there's nothing between
    /// them to check.
    /// </summary>
    public static class LineOfSight
    {
        public static bool HasClearPath(GridPosition from, GridPosition to, IWalkableMap map)
        {
            foreach (var tile in TilesBetween(from, to))
            {
                if (tile.Equals(from) || tile.Equals(to)) continue;
                if (!map.IsWalkable(tile)) return false;
            }
            return true;
        }

        private static IEnumerable<GridPosition> TilesBetween(GridPosition from, GridPosition to)
        {
            int x0 = from.X, y0 = from.Y;
            int x1 = to.X, y1 = to.Y;

            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            int x = x0, y = y0;
            while (true)
            {
                yield return new GridPosition(x, y);
                if (x == x1 && y == y1) yield break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }
    }
}
