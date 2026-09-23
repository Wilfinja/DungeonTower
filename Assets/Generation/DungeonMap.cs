using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// A simple 2D grid of tiles. Implements IWalkableMap so Combat can
    /// query it directly without this assembly depending on Combat, or
    /// Combat depending on this assembly.
    /// </summary>
    public sealed class DungeonMap : IWalkableMap
    {
        private readonly TileType[,] _tiles;

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
        }

        public bool InBounds(GridPosition position)
            => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        public bool IsWalkable(GridPosition position)
            => InBounds(position) && GetTile(position) == TileType.Floor;
    }
}
