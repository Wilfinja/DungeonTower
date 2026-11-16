using System;
using System.IO;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Reads and writes the single save file, and hands a loaded save from
    /// the title screen to the game scene (PendingLoad). Everything that
    /// can go wrong comes back as a false/null plus a Console message, so a
    /// damaged save can never crash the game.
    ///
    /// The file lives in Application.persistentDataPath (the same place a
    /// Steam cloud-save rule can point at later). It's written to a
    /// temporary file first and swapped in, so a crash mid-save leaves the
    /// previous save intact.
    /// </summary>
    public static class SaveService
    {
        private const string FileName = "savegame.json";

        /// <summary>Set by the title screen's Continue button; BattleController.Start() takes it.</summary>
        public static SaveData PendingLoad { get; set; }

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists() => File.Exists(FilePath);

        public static bool Write(SaveData data)
        {
            try
            {
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data));

                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
                else File.Move(temp, FilePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService: couldn't write {FilePath}: {e.Message}");
                return false;
            }
        }

        /// <summary>The saved game, or null if there is none or it can't be used.</summary>
        public static SaveData Read()
        {
            if (!Exists()) return null;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data == null || data.heroes == null || data.heroes.Count == 0 || string.IsNullOrEmpty(data.tiles))
                {
                    Debug.LogError("SaveService: the save file is empty or damaged.");
                    return null;
                }
                if (data.version != SaveData.CurrentVersion)
                {
                    Debug.LogError($"SaveService: the save is version {data.version} but this game reads version {SaveData.CurrentVersion}.");
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService: couldn't read {FilePath}: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveService: couldn't delete {FilePath}: {e.Message}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => PendingLoad = null;
    }
}
