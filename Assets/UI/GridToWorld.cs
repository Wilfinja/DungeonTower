using UnityEngine;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// Converts between grid coordinates and Unity world space. One place
    /// to change if tile size or orientation needs adjusting later.
    /// </summary>
    public static class GridToWorld
    {
        public const float TileSize = 1f;

        public static Vector3 ToWorldPosition(GridPosition gridPosition)
            => new Vector3(gridPosition.X * TileSize, gridPosition.Y * TileSize, 0f);
    }
}
