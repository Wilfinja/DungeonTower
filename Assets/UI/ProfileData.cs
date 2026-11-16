using System;
using System.Collections.Generic;

namespace DungeonTower.UI
{
    /// <summary>
    /// Everything that survives between runs: the town's progress. Lives in
    /// its own file (profile.json), separate from the run save, so
    /// abandoning or losing a run never touches it.
    /// </summary>
    [Serializable]
    public sealed class ProfileData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        // Gems: earned in the dungeon, spent in town. Never reset.
        public int gems;

        // Hero types recruited with gems (types that start unlocked aren't listed).
        public List<string> unlockedHeroTypes = new List<string>();

        // The party chosen at the tavern for the next run, by hero type id.
        public List<string> party = new List<string>();

        // One level per upgrade key, for the blacksmith, potion maker and
        // arcanist. A key with no entry is level 0. Keeping them as plain
        // key/level pairs means adding an upgrade never changes this file's layout.
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
    }

    [Serializable]
    public struct UpgradeLevel
    {
        public string key;
        public int level;
    }
}
