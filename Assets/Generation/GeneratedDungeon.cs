using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// A generated map plus the rooms carved into it, in placement order.
    /// Also answers "which tiles are corridor" (walkable but not inside
    /// any room) — derived on demand rather than tracked separately during
    /// carving, so there's only one source of truth for room boundaries.
    /// </summary>
    public sealed class GeneratedDungeon
    {
        public DungeonMap Map { get; }
        public IReadOnlyList<Room> Rooms { get; }

        public GeneratedDungeon(DungeonMap map, IReadOnlyList<Room> rooms)
        {
            Map = map;
            Rooms = rooms;
        }

        public IEnumerable<GridPosition> GetCorridorTiles()
        {
            for (int x = 0; x < Map.Width; x++)
            {
                for (int y = 0; y < Map.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (Map.IsWalkable(pos) && !Rooms.Any(r => r.Contains(pos)))
                        yield return pos;
                }
            }
        }

        public HashSet<GridPosition> CorridorTilesNear(GridPosition anchor, int radius)
        {
            var result = new HashSet<GridPosition>();
            foreach (var tile in GetCorridorTiles())
                if (tile.ManhattanDistance(anchor) <= radius)
                    result.Add(tile);
            return result;
        }
    }
}
