using System;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Player-facing options, saved in PlayerPrefs so they survive between
    /// sessions and are shared by the title screen and the pause menu.
    /// Anything can read these; the Options screen is the only thing that
    /// writes them. Changed fires after any write.
    ///
    /// Master volume is applied right away through AudioListener.volume.
    /// Music and SFX are stored and ready, but nothing in the game plays
    /// music yet — when the audio arrives, read MusicVolume / SfxVolume
    /// (or hand them to an AudioMixer) wherever you play sounds. The hit
    /// sounds in HitFeedback already respect SfxVolume.
    /// </summary>
    public static class GameSettings
    {
        private const string MasterKey = "settings.volume.master";
        private const string MusicKey = "settings.volume.music";
        private const string SfxKey = "settings.volume.sfx";
        private const string ShakeKey = "settings.screenShake";
        private const string FastCombatKey = "settings.fastCombat";

        // How much faster everything plays with Fast Combat on.
        public const float FastCombatSpeed = 2f;

        public static event Action Changed;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterKey, 1f);
            set { SetFloat(MasterKey, value); ApplyAudio(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 1f);
            set => SetFloat(MusicKey, value);
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set => SetFloat(SfxKey, value);
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) != 0;
            set => SetBool(ShakeKey, value);
        }

        // Skips wind-up/charge effects and speeds up walking, lunges and
        // projectiles. Damage, hit effects and turn order are unchanged.
        public static bool FastCombat
        {
            get => PlayerPrefs.GetInt(FastCombatKey, 0) != 0;
            set => SetBool(FastCombatKey, value);
        }

        /// <summary>1 normally, FastCombatSpeed with Fast Combat on. Multiply speeds by it.</summary>
        public static float CombatSpeed => FastCombat ? FastCombatSpeed : 1f;

        /// <summary>A duration shortened to match CombatSpeed. Use it for fixed waits.</summary>
        public static float CombatSeconds(float seconds) => seconds / CombatSpeed;

        public static void ApplyAudio() => AudioListener.volume = Mathf.Clamp01(MasterVolume);

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnLoad() => ApplyAudio();
    }
}
