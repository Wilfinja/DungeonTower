using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// A placed room's bounds. Used during generation for overlap checks,
    /// and afterward for confining room-roaming enemies to their own room
    /// and telling room tiles apart from corridor tiles.
    /// </summary>
    public readonly struct Room
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public Room(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public GridPosition Center => new GridPosition(X + Width / 2, Y + Height / 2);

        public bool Contains(GridPosition position)
            => position.X >= X && position.X < X + Width && position.Y >= Y && position.Y < Y + Height;

        public IEnumerable<GridPosition> Tiles()
        {
            for (int x = X; x < X + Width; x++)
                for (int y = Y; y < Y + Height; y++)
                    yield return new GridPosition(x, y);
        }

        public bool Overlaps(Room other, int padding)
        {
            return X - padding < other.X + other.Width
                && X + Width + padding > other.X
                && Y - padding < other.Y + other.Height
                && Y + Height + padding > other.Y;
        }
    }
}
