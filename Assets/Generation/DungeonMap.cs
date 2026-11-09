using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// A simple 2D grid of tiles. Implements IWalkableMap so Combat can
    /// query it directly without this assembly depending on Combat, or
    /// Combat depending on this assembly.
    ///
    /// A closed door blocks sight, and blocks movement too once Combat's
    /// MovementMapView wraps this map (IsWalkable here stays true so level
    /// generation can still treat doorways as connected). Doors are opened
    /// and closed from an adjacent tile (see BattleController).
    /// </summary>
    public sealed class DungeonMap : IWalkableMap, ISightBlockingMap
    {
        private readonly TileType[,] _tiles;
        private readonly HashSet<GridPosition> _openDoors = new HashSet<GridPosition>();

        public int Width { get; }
        public int Height { get; }

        public DungeonMap(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new TileType[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _tiles[x, y] = TileType.Wall;
        }

        public TileType GetTile(GridPosition position) => _tiles[position.X, position.Y];

        public void SetTile(GridPosition position, TileType tile)
        {
            _tiles[position.X, position.Y] = tile;
            if (tile != TileType.Door) _openDoors.Remove(position);
        }

        public bool InBounds(GridPosition position)
            => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        // Everything except a wall can be walked on.
        public bool IsWalkable(GridPosition position)
            => InBounds(position) && GetTile(position) != TileType.Wall;

        public bool IsDoor(GridPosition position)
            => InBounds(position) && GetTile(position) == TileType.Door;

        public bool IsDoorOpen(GridPosition position) => _openDoors.Contains(position);

        /// <summary>Opens the door at this position. True only if a closed door was just opened.</summary>
        public bool TryOpenDoor(GridPosition position)
            => IsDoor(position) && _openDoors.Add(position);

        /// <summary>Closes the door at this position. True only if an open door was just closed.</summary>
        public bool TryCloseDoor(GridPosition position)
            => IsDoor(position) && _openDoors.Remove(position);

        public bool BlocksSight(GridPosition position)
        {
            if (!InBounds(position)) return true;
            var tile = GetTile(position);
            return tile == TileType.Wall
                || (tile == TileType.Door && !_openDoors.Contains(position));
        }
    }
}
