using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// Hands out starting tiles: players near the first room placed,
    /// enemies near the last (or near an arbitrary point, for corridor
    /// groups). Every search excludes whatever tiles were already claimed,
    /// so no two calls can land on the same tile.
    /// </summary>
    public static class SpawnZones
    {
        public static List<GridPosition> PlayerSpawns(GeneratedDungeon dungeon, int count)
            => SpawnsNear(dungeon.Map, dungeon.Rooms.First().Center, count, null);

        public static List<GridPosition> EnemySpawns(
            GeneratedDungeon dungeon, int count, IReadOnlyCollection<GridPosition> exclude)
            => SpawnsNear(dungeon.Map, dungeon.Rooms.Last().Center, count, exclude);

        public static List<GridPosition> SpawnsNearPosition(
            GeneratedDungeon dungeon, GridPosition center, int count, IReadOnlyCollection<GridPosition> exclude)
            => SpawnsNear(dungeon.Map, center, count, exclude);

        // A corridor tile near the map's overall center, to anchor a
        // corridor-roaming group somewhere central rather than tucked in
        // a corner. Null if the map has no corridor tiles at all (e.g.
        // the single-room fallback, which has none).
        public static GridPosition? PickCorridorAnchor(GeneratedDungeon dungeon)
        {
            var mapCenter = new GridPosition(dungeon.Map.Width / 2, dungeon.Map.Height / 2);
            var ordered = dungeon.GetCorridorTiles().OrderBy(t => t.ManhattanDistance(mapCenter));
            foreach (var tile in ordered) return tile;
            return null;
        }

        private static List<GridPosition> SpawnsNear(
            DungeonMap map, GridPosition center, int count, IReadOnlyCollection<GridPosition> exclude)
        {
            var excluded = exclude ?? Array.Empty<GridPosition>();
            var visited = new HashSet<GridPosition>();
            var result = new List<GridPosition>();
            var frontier = new Queue<GridPosition>();

            if (map.IsWalkable(center))
            {
                visited.Add(center);
                frontier.Enqueue(center);
            }

            while (frontier.Count > 0 && result.Count < count)
            {
                var current = frontier.Dequeue();
                if (!Contains(excluded, current))
                    result.Add(current);

                foreach (var neighbor in current.Neighbors())
                {
                    if (visited.Contains(neighbor)) continue;
                    if (!map.IsWalkable(neighbor)) continue;
                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }

            return result;
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
