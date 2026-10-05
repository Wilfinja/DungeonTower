using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Generation
{
    /// <summary>
    /// Map generation, driven entirely by MapGenSettings.
    ///
    /// GenerateProcedural places non-overlapping rectangular rooms, joins
    /// each new room to the previous one (so everything is reachable),
    /// optionally adds extra loop corridors, then puts doors in the
    /// openings and an exit in the room farthest from the start.
    ///
    /// Corridors leave a room straight out of one wall, run through the
    /// gap between the rooms (with one sideways jog if the doorways don't
    /// line up) and enter the other room straight through its facing wall.
    /// Because every corridor starts and ends at a wall opening, a room
    /// is only ever open to the outside at its doorways — that's what
    /// lets doors really separate rooms. A corridor that would pass next
    /// to a third room is rejected and retried, and a room that can't be
    /// connected is simply not placed.
    ///
    /// GenerateSingleRoom is kept as a fallback and a simple baseline for
    /// isolating combat-feel issues from generation issues.
    /// </summary>
    public static class MapGenerator
    {
        private const int ConnectAttempts = 12;
        // Two openings must be at least this many tiles apart (Chebyshev)
        // so doors never merge into one giant doorway.
        private const int DoorClearance = 2;

        // ---------------- Public entry points ----------------

        public static GeneratedDungeon GenerateSingleRoom(int width, int height)
            => GenerateSingleRoom(new MapGenSettings { MapWidth = width, MapHeight = height });

        public static GeneratedDungeon GenerateSingleRoom(MapGenSettings settings)
        {
            var s = settings.Sanitized();
            var map = new DungeonMap(s.MapWidth, s.MapHeight);

            for (int x = 0; x < s.MapWidth; x++)
            {
                for (int y = 0; y < s.MapHeight; y++)
                {
                    bool isBorder = x == 0 || y == 0 || x == s.MapWidth - 1 || y == s.MapHeight - 1;
                    map.SetTile(new GridPosition(x, y), isBorder ? TileType.Wall : TileType.Floor);
                }
            }

            var interior = new Room(1, 1, s.MapWidth - 2, s.MapHeight - 2);
            var exit = PlaceExit(map, interior, s.ExitSize, new List<GridPosition>());
            return new GeneratedDungeon(map, new List<Room> { interior }, exit);
        }

        // Legacy signature, kept so existing callers/tests still compile.
        public static GeneratedDungeon GenerateProcedural(int width, int height, int? seed = null)
            => GenerateProcedural(new MapGenSettings
            {
                MapWidth = width,
                MapHeight = height,
                UseFixedSeed = seed.HasValue,
                Seed = seed ?? 0
            });

        public static GeneratedDungeon GenerateProcedural(MapGenSettings settings)
        {
            var s = settings.Sanitized();
            var rng = s.UseFixedSeed ? new Random(s.Seed) : new Random();
            var map = new DungeonMap(s.MapWidth, s.MapHeight);
            var rooms = new List<Room>();
            var openings = new List<GridPosition[]>(); // one entry per doorway (corridor-width tiles)

            for (int attempt = 0; attempt < s.MaxPlacementAttempts && rooms.Count < s.MaxRooms; attempt++)
            {
                int roomWidth = rng.Next(s.MinRoomSize, s.MaxRoomSize + 1);
                int roomHeight = rng.Next(s.MinRoomSize, s.MaxRoomSize + 1);
                int maxX = s.MapWidth - roomWidth - 1;
                int maxY = s.MapHeight - roomHeight - 1;
                if (maxX < 1 || maxY < 1) continue; // room doesn't fit this map size at all

                var candidate = new Room(rng.Next(1, maxX + 1), rng.Next(1, maxY + 1), roomWidth, roomHeight);
                if (rooms.Exists(r => r.Overlaps(candidate, s.RoomSpacing))) continue;

                // Every room after the first must connect to the one before
                // it, or it isn't placed at all (so nothing is ever cut off).
                if (rooms.Count > 0
                    && !TryConnect(map, rooms, rooms[rooms.Count - 1], candidate, s, rng, openings))
                    continue;

                CarveRoom(map, candidate);
                rooms.Add(candidate);
            }

            // Fail-safe: if placement somehow never succeeded (shouldn't
            // happen at reasonable map sizes), fall back rather than hand
            // back an all-wall map with nowhere to spawn anyone.
            if (rooms.Count == 0)
                return GenerateSingleRoom(s);

            // Loops: connect a few rooms that aren't chain neighbours.
            // Failures are fine — it's a bonus, not a requirement.
            if (rooms.Count > 2)
            {
                for (int i = 0; i < s.ExtraConnections; i++)
                {
                    int ia = rng.Next(rooms.Count);
                    int ib = rng.Next(rooms.Count);
                    if (Math.Abs(ia - ib) < 2) continue;
                    TryConnect(map, rooms, rooms[ia], rooms[ib], s, rng, openings);
                }
            }

            // Doors go in after all carving so nothing can overwrite them.
            foreach (var opening in openings)
            {
                bool makeDoor = s.DoorsEnabled && rng.NextDouble() < s.DoorChance;
                if (!makeDoor) continue; // stays an open archway

                foreach (var tile in opening)
                {
                    map.SetTile(tile, TileType.Door);
                    if (s.DoorsStartOpen) map.TryOpenDoor(tile);
                }
            }

            var allOpeningTiles = new List<GridPosition>();
            foreach (var opening in openings) allOpeningTiles.AddRange(opening);

            var exitRoom = FindExitRoom(map, rooms);
            var exitTiles = PlaceExit(map, exitRoom, s.ExitSize, allOpeningTiles);

            return new GeneratedDungeon(map, rooms, exitTiles);
        }

        // ---------------- Rooms / exit ----------------

        private static void CarveRoom(DungeonMap map, Room room)
        {
            foreach (var pos in room.Tiles())
                map.SetTile(pos, TileType.Floor);
        }

        // The room with the longest walking distance from the first room's
        // centre. Uses real path distance (not straight-line) so loops and
        // winding chains are handled properly.
        private static Room FindExitRoom(DungeonMap map, List<Room> rooms)
        {
            if (rooms.Count == 1) return rooms[0];

            var distance = new Dictionary<GridPosition, int>();
            var frontier = new Queue<GridPosition>();
            var start = rooms[0].Center;
            distance[start] = 0;
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var neighbor in current.Neighbors())
                {
                    if (distance.ContainsKey(neighbor) || !map.IsWalkable(neighbor)) continue;
                    distance[neighbor] = distance[current] + 1;
                    frontier.Enqueue(neighbor);
                }
            }

            var best = rooms[rooms.Count - 1];
            int bestDistance = -1;
            for (int i = 1; i < rooms.Count; i++)
            {
                if (distance.TryGetValue(rooms[i].Center, out int d) && d > bestDistance)
                {
                    best = rooms[i];
                    bestDistance = d;
                }
            }
            return best;
        }

        // Puts a size x size block of Exit tiles in whichever corner of the
        // room is farthest from any doorway, so you don't step on the exit
        // the moment you walk in.
        private static List<GridPosition> PlaceExit(
            DungeonMap map, Room room, int size, IReadOnlyList<GridPosition> openingTiles)
        {
            size = Math.Max(1, Math.Min(size, Math.Min(room.Width, room.Height)));

            var corners = new[]
            {
                new GridPosition(room.X, room.Y),
                new GridPosition(room.X + room.Width - size, room.Y),
                new GridPosition(room.X, room.Y + room.Height - size),
                new GridPosition(room.X + room.Width - size, room.Y + room.Height - size)
            };

            var best = corners[0];
            int bestScore = int.MinValue;
            foreach (var corner in corners)
            {
                var middle = new GridPosition(corner.X + size / 2, corner.Y + size / 2);
                int score = int.MaxValue;
                foreach (var opening in openingTiles)
                    score = Math.Min(score, middle.ManhattanDistance(opening));

                if (score > bestScore)
                {
                    bestScore = score;
                    best = corner;
                }
            }

            var tiles = new List<GridPosition>();
            for (int dx = 0; dx < size; dx++)
            {
                for (int dy = 0; dy < size; dy++)
                {
                    var pos = new GridPosition(best.X + dx, best.Y + dy);
                    map.SetTile(pos, TileType.Exit);
                    tiles.Add(pos);
                }
            }
            return tiles;
        }

        // ---------------- Corridors ----------------

        private readonly struct TileRect
        {
            public readonly int MinX, MinY, MaxX, MaxY;
            public TileRect(int minX, int minY, int maxX, int maxY)
            {
                MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
            }
        }

        private static bool TryConnect(
            DungeonMap map, List<Room> rooms, Room a, Room b,
            MapGenSettings s, Random rng, List<GridPosition[]> openings)
        {
            for (int attempt = 0; attempt < ConnectAttempts; attempt++)
            {
                if (!TryPlanCorridor(map, rooms, a, b, s, rng, openings,
                        out var rects, out var doorA, out var doorB))
                    continue;

                foreach (var rect in rects) CarveRect(map, rect);
                openings.Add(doorA);
                openings.Add(doorB);
                return true;
            }
            return false;
        }

        // Plans (but doesn't carve) one corridor between two rooms.
        //
        // Everything is worked out in "forward" (the axis the rooms are
        // separated along) and "lateral" (the other axis) coordinates, so
        // the horizontal and vertical cases share one code path. Layout,
        // with the rooms in forward order A then B:
        //
        //   [ A ] D==========+            +==========D [ B ]
        //                    |  (jog)     |
        //                    +============+
        //
        // D is the doorway, sitting in the room's wall line. If A and B
        // overlap sideways enough, both doorways line up and the corridor
        // is dead straight.
        private static bool TryPlanCorridor(
            DungeonMap map, List<Room> rooms, Room a, Room b,
            MapGenSettings s, Random rng, List<GridPosition[]> openings,
            out List<TileRect> rects, out GridPosition[] doorA, out GridPosition[] doorB)
        {
            rects = null;
            doorA = null;
            doorB = null;

            bool horizontal = a.X + a.Width + s.RoomSpacing <= b.X
                           || b.X + b.Width + s.RoomSpacing <= a.X;

            int aF0 = horizontal ? a.X : a.Y;
            int aF1 = horizontal ? a.X + a.Width : a.Y + a.Height;
            int aL0 = horizontal ? a.Y : a.X;
            int aL1 = horizontal ? a.Y + a.Height : a.X + a.Width;
            int bF0 = horizontal ? b.X : b.Y;
            int bF1 = horizontal ? b.X + b.Width : b.Y + b.Height;
            int bL0 = horizontal ? b.Y : b.X;
            int bL1 = horizontal ? b.Y + b.Height : b.X + b.Width;

            // Make "A" the room that comes first along the forward axis.
            if (bF1 <= aF0)
            {
                (aF0, bF0) = (bF0, aF0);
                (aF1, bF1) = (bF1, aF1);
                (aL0, bL0) = (bL0, aL0);
                (aL1, bL1) = (bL1, aL1);
            }

            int width = rng.Next(s.MinCorridorWidth, s.MaxCorridorWidth + 1);
            int aWall = aF1;      // wall column/row of A facing B (doorway goes here)
            int bWall = bF0 - 1;  // wall column/row of B facing A

            // The sideways jog must sit strictly between the two doorway
            // lines, so it never runs along either room's wall.
            int jogMin = aWall + 1;
            int jogMax = bWall - width;
            if (jogMax < jogMin) return false;
            if (aL1 - aL0 < width || bL1 - bL0 < width) return false;

            int overlapStart = Math.Max(aL0, bL0);
            int overlapEnd = Math.Min(aL1, bL1);
            int latA, latB;
            if (overlapEnd - overlapStart >= width)
            {
                latA = latB = rng.Next(overlapStart, overlapEnd - width + 1); // straight shot
            }
            else
            {
                latA = rng.Next(aL0, aL1 - width + 1);
                latB = rng.Next(bL0, bL1 - width + 1);
            }
            int jog = rng.Next(jogMin, jogMax + 1);

            var plan = new List<TileRect>
            {
                MakeRect(horizontal, aWall, jog + width - 1, latA, latA + width - 1),
                MakeRect(horizontal, jog, jog + width - 1,
                    Math.Min(latA, latB), Math.Max(latA, latB) + width - 1),
                MakeRect(horizontal, jog, bWall, latB, latB + width - 1)
            };

            var newDoorA = new GridPosition[width];
            var newDoorB = new GridPosition[width];
            for (int k = 0; k < width; k++)
            {
                newDoorA[k] = ToPosition(horizontal, aWall, latA + k);
                newDoorB[k] = ToPosition(horizontal, bWall, latB + k);
            }

            // Doorways must not crowd existing ones.
            foreach (var existing in openings)
                foreach (var tile in existing)
                    foreach (var candidate in newDoorA)
                        if (Chebyshev(tile, candidate) <= DoorClearance) return false;
            foreach (var existing in openings)
                foreach (var tile in existing)
                    foreach (var candidate in newDoorB)
                        if (Chebyshev(tile, candidate) <= DoorClearance) return false;

            // Stay on the map, and keep a wall tile between the corridor
            // and every room other than the two being joined.
            foreach (var rect in plan)
            {
                if (rect.MinX < 0 || rect.MinY < 0
                    || rect.MaxX >= map.Width || rect.MaxY >= map.Height) return false;

                foreach (var room in rooms)
                {
                    if (room.Equals(a) || room.Equals(b)) continue;
                    bool touches = rect.MinX <= room.X + room.Width
                                && rect.MaxX >= room.X - 1
                                && rect.MinY <= room.Y + room.Height
                                && rect.MaxY >= room.Y - 1;
                    if (touches) return false;
                }
            }

            rects = plan;
            doorA = newDoorA;
            doorB = newDoorB;
            return true;
        }

        private static TileRect MakeRect(bool horizontal, int f0, int f1, int l0, int l1)
            => horizontal ? new TileRect(f0, l0, f1, l1) : new TileRect(l0, f0, l1, f1);

        private static GridPosition ToPosition(bool horizontal, int forward, int lateral)
            => horizontal ? new GridPosition(forward, lateral) : new GridPosition(lateral, forward);

        private static int Chebyshev(GridPosition p, GridPosition q)
            => Math.Max(Math.Abs(p.X - q.X), Math.Abs(p.Y - q.Y));

        // Only turns walls into floor, so carving never damages existing
        // floor, doors or exits.
        private static void CarveRect(DungeonMap map, TileRect rect)
        {
            for (int x = rect.MinX; x <= rect.MaxX; x++)
            {
                for (int y = rect.MinY; y <= rect.MaxY; y++)
                {
                    var pos = new GridPosition(x, y);
                    if (map.GetTile(pos) == TileType.Wall)
                        map.SetTile(pos, TileType.Floor);
                }
            }
        }
    }
}
