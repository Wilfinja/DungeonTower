using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// A generated map plus the rooms carved into it, in placement order,
    /// plus the tiles that make up the exit down to the next floor.
    /// Also answers "which tiles are corridor" (walkable but not inside
    /// any room) — derived on demand rather than tracked separately during
    /// carving, so there's only one source of truth for room boundaries.
    /// Door tiles count as corridor tiles (they sit in the room's wall
    /// line, outside the room's floor rectangle).
    /// </summary>
    public sealed class GeneratedDungeon
    {
        public DungeonMap Map { get; }
        public IReadOnlyList<Room> Rooms { get; }
        public IReadOnlyList<GridPosition> ExitTiles { get; }

        public GeneratedDungeon(
            DungeonMap map, IReadOnlyList<Room> rooms, IReadOnlyList<GridPosition> exitTiles = null)
        {
            Map = map;
            Rooms = rooms;
            ExitTiles = exitTiles ?? new List<GridPosition>();
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
