using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// Map generation. GenerateProcedural is the real thing — non-
    /// overlapping rectangular rooms connected by L-shaped corridors.
    /// GenerateSingleRoom is kept around as a fallback and a simple
    /// baseline for isolating combat-feel issues from generation issues.
    /// </summary>
    public static class MapGenerator
    {
        private const int MaxRooms = 5;
        private const int MinRoomSize = 3;
        private const int MaxRoomSize = 6;
        private const int MaxPlacementAttempts = 40;
        private const int RoomPadding = 1; // minimum gap kept between rooms

        public static GeneratedDungeon GenerateSingleRoom(int width, int height)
        {
            var map = new DungeonMap(width, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    map.SetTile(new GridPosition(x, y), isBorder ? TileType.Wall : TileType.Floor);
                }
            }

            var interior = new Room(1, 1, width - 2, height - 2);
            return new GeneratedDungeon(map, new List<Room> { interior });
        }

        public static GeneratedDungeon GenerateProcedural(int width, int height, int? seed = null)
        {
            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var map = new DungeonMap(width, height);
            var rooms = new List<Room>();

            for (int attempt = 0; attempt < MaxPlacementAttempts && rooms.Count < MaxRooms; attempt++)
            {
                int roomWidth = rng.Next(MinRoomSize, MaxRoomSize + 1);
                int roomHeight = rng.Next(MinRoomSize, MaxRoomSize + 1);
                int maxX = width - roomWidth - 1;
                int maxY = height - roomHeight - 1;
                if (maxX < 1 || maxY < 1) continue; // room doesn't fit this map size at all

                var candidate = new Room(rng.Next(1, maxX + 1), rng.Next(1, maxY + 1), roomWidth, roomHeight);
                if (rooms.Exists(r => r.Overlaps(candidate, RoomPadding))) continue;

                CarveRoom(map, candidate);
                if (rooms.Count > 0)
                    CarveCorridor(map, rooms[rooms.Count - 1].Center, candidate.Center, rng);

                rooms.Add(candidate);
            }

            // Fail-safe: if placement somehow never succeeded (shouldn't
            // happen at reasonable map sizes), fall back rather than hand
            // back an all-wall map with nowhere to spawn anyone.
            if (rooms.Count == 0)
                return GenerateSingleRoom(width, height);

            return new GeneratedDungeon(map, rooms);
        }

        private static void CarveRoom(DungeonMap map, Room room)
        {
            foreach (var pos in room.Tiles())
                map.SetTile(pos, TileType.Floor);
        }

        private static void CarveCorridor(DungeonMap map, GridPosition from, GridPosition to, Random rng)
        {
            // Randomize whether the corridor bends horizontal-then-vertical
            // or vertical-then-horizontal, purely for visual variety.
            if (rng.Next(2) == 0)
            {
                CarveHorizontal(map, from.X, to.X, from.Y);
                CarveVertical(map, from.Y, to.Y, to.X);
            }
            else
            {
                CarveVertical(map, from.Y, to.Y, from.X);
                CarveHorizontal(map, from.X, to.X, to.Y);
            }
        }

        private static void CarveHorizontal(DungeonMap map, int fromX, int toX, int y)
        {
            for (int x = Math.Min(fromX, toX); x <= Math.Max(fromX, toX); x++)
                map.SetTile(new GridPosition(x, y), TileType.Floor);
        }

        private static void CarveVertical(DungeonMap map, int fromY, int toY, int x)
        {
            for (int y = Math.Min(fromY, toY); y <= Math.Max(fromY, toY); y++)
                map.SetTile(new GridPosition(x, y), TileType.Floor);
        }
    }
}
