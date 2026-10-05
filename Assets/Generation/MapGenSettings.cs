using System;

namespace DungeonTower.Generation
{
    /// <summary>
    /// Every knob for procedural map generation in one plain serializable
    /// class, so BattleController can expose it straight in the Inspector:
    ///     [SerializeField] private MapGenSettings _mapGen = new MapGenSettings();
    ///
    /// Public fields on purpose (same reason as DungeonThemeSO's entry
    /// structs) and no UnityEngine attributes, so the Generation assembly
    /// stays engine-free. Nothing here can be set to a value that breaks
    /// generation: MapGenerator always works from Sanitized(), which clamps
    /// everything into a consistent range (e.g. rooms always fit the map,
    /// corridors always fit between rooms).
    /// </summary>
    [Serializable]
    public sealed class MapGenSettings
    {
        // ---------------- Map ----------------
        public int MapWidth = 48;
        public int MapHeight = 32;
        // Tick this on to get the same map every run (handy for reproducing
        // a bug); leave off for a fresh map every time.
        public bool UseFixedSeed = false;
        public int Seed = 12345;

        // ---------------- Rooms ----------------
        public int MaxRooms = 6;
        public int MinRoomSize = 6;
        public int MaxRoomSize = 10;
        // Minimum wall gap between two rooms. Raised automatically if it
        // would be too small for the widest corridor to fit (needs
        // corridor width + 2).
        public int RoomSpacing = 4;
        public int MaxPlacementAttempts = 120;
        // Extra corridors between rooms that aren't neighbours in the main
        // chain. 0 = a single winding path; higher = loops and shortcuts.
        public int ExtraConnections = 1;

        // ---------------- Corridors ----------------
        // Each corridor picks a width between these (inclusive).
        public int MinCorridorWidth = 2;
        public int MaxCorridorWidth = 2;

        // ---------------- Doors ----------------
        public bool DoorsEnabled = true;
        // 0..1 chance that a room opening gets a door; the rest stay open
        // archways.
        public float DoorChance = 1f;
        public bool DoorsStartOpen = false;

        // ---------------- Exit ----------------
        // The exit is a square of stairs tiles (ExitSize x ExitSize) in
        // the room farthest from the start.
        public int ExitSize = 2;

        /// <summary>
        /// A clamped copy that's safe to generate from. The original is
        /// never modified, so the Inspector never "fights" you.
        /// </summary>
        public MapGenSettings Sanitized()
        {
            var s = (MapGenSettings)MemberwiseClone();

            s.MapWidth = Math.Max(8, s.MapWidth);
            s.MapHeight = Math.Max(8, s.MapHeight);

            s.MaxRoomSize = Math.Max(3, Math.Min(s.MaxRoomSize, Math.Min(s.MapWidth, s.MapHeight) - 2));
            s.MinRoomSize = Math.Max(3, Math.Min(s.MinRoomSize, s.MaxRoomSize));

            s.ExitSize = Math.Max(1, Math.Min(s.ExitSize, s.MinRoomSize));

            s.MaxCorridorWidth = Math.Max(1, Math.Min(s.MaxCorridorWidth, s.MinRoomSize));
            s.MinCorridorWidth = Math.Max(1, Math.Min(s.MinCorridorWidth, s.MaxCorridorWidth));

            s.RoomSpacing = Math.Max(s.RoomSpacing, s.MaxCorridorWidth + 2);

            s.MaxRooms = Math.Max(1, s.MaxRooms);
            s.MaxPlacementAttempts = Math.Max(1, s.MaxPlacementAttempts);
            s.ExtraConnections = Math.Max(0, s.ExtraConnections);
            s.DoorChance = Math.Min(1f, Math.Max(0f, s.DoorChance));

            return s;
        }
    }
}
