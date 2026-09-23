using System;
using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// A tile coordinate on the combat grid. Pure C# (not UnityEngine.Vector2Int)
    /// so Core stays engine-agnostic.
    /// </summary>
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int ManhattanDistance(GridPosition other)
            => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public IEnumerable<GridPosition> Neighbors()
        {
            yield return new GridPosition(X + 1, Y);
            yield return new GridPosition(X - 1, Y);
            yield return new GridPosition(X, Y + 1);
            yield return new GridPosition(X, Y - 1);
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => (X, Y).GetHashCode();
        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y})";
    }
}
