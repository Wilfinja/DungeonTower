using System;
using System.IO;
using DungeonTower.Items;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Reads and writes the town progress file (profile.json) and answers
    /// the questions the town and dungeon ask of it: how many gems, which
    /// hero types are unlocked, what level an upgrade is at. Every change
    /// that matters is written straight to disk, so quitting at any moment
    /// loses nothing.
    ///
    /// Same location and safe-write approach as SaveService. A damaged
    /// profile is set aside as profile.corrupt.json rather than deleted, and
    /// a fresh one is started, so a bad file can never block the game.
    /// </summary>
    public static class ProfileService
    {
        private const string FileName = "profile.json";
        private const string CorruptFileName = "profile.corrupt.json";

        private static ProfileData _data;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static ProfileData Data
        {
            get
            {
                if (_data == null) _data = Load();
                return _data;
            }
        }

        public static int Gems => Data.gems;

        // ---- Gems -----------------------------------------------------------

        public static void AddGems(int amount)
        {
            if (amount <= 0) return;
            Data.gems += amount;
            Save();
            Debug.Log($"+{amount} gems ({Data.gems} total).");
        }

        public static bool TrySpendGems(int amount)
        {
            if (amount < 0 || Data.gems < amount) return false;
            Data.gems -= amount;
            Save();
            return true;
        }

        // ---- Hero types -----------------------------------------------------

        public static bool IsUnlocked(HeroTypeSO type)
            => type != null && (type.StartsUnlocked || Data.unlockedHeroTypes.Contains(type.Id));

        /// <summary>Pays the type's gem cost and unlocks it. False if it's already unlocked or you can't afford it.</summary>
        public static bool TryRecruit(HeroTypeSO type)
        {
            if (type == null || IsUnlocked(type)) return false;
            if (!TrySpendGems(type.GemCost)) return false;

            Data.unlockedHeroTypes.Add(type.Id);
            Save();
            return true;
        }

        // ---- Upgrades -------------------------------------------------------

        public static int GetUpgradeLevel(string key)
        {
            foreach (var entry in Data.upgrades)
                if (entry.key == key) return entry.level;
            return 0;
        }

        public static void SetUpgradeLevel(string key, int level)
        {
            for (int i = 0; i < Data.upgrades.Count; i++)
            {
                if (Data.upgrades[i].key != key) continue;
                Data.upgrades[i] = new UpgradeLevel { key = key, level = level };
                Save();
                return;
            }
            Data.upgrades.Add(new UpgradeLevel { key = key, level = level });
            Save();
        }

        // ---- File -----------------------------------------------------------

        public static bool Save()
        {
            try
            {
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(Data));

                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"ProfileService: couldn't write {FilePath}: {e.Message}");
                return false;
            }
        }

        private static ProfileData Load()
        {
            if (!File.Exists(FilePath)) return new ProfileData();

            try
            {
                var data = JsonUtility.FromJson<ProfileData>(File.ReadAllText(FilePath));
                if (data != null && data.version <= ProfileData.CurrentVersion) return data;
                Debug.LogError("ProfileService: the profile is from a newer version of the game or is empty.");
            }
            catch (Exception e)
            {
                Debug.LogError($"ProfileService: couldn't read {FilePath}: {e.Message}");
            }

            SetAsideCorruptFile();
            return new ProfileData();
        }

        private static void SetAsideCorruptFile()
        {
            try
            {
                var target = Path.Combine(Application.persistentDataPath, CorruptFileName);
                if (File.Exists(target)) File.Delete(target);
                File.Move(FilePath, target);
                Debug.LogWarning($"ProfileService: the old profile was kept as {target}; starting a new one.");
            }
            catch (Exception e)
            {
                Debug.LogError($"ProfileService: couldn't set the damaged profile aside: {e.Message}");
            }
        }

        // With Reload Domain off, statics would otherwise leak between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _data = null;
    }
}
