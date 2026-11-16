using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// The one place that knows whether the game is paused. Pausing sets
    /// Time.timeScale to 0, which freezes every scaled-time animation
    /// (walks, lunges, projectiles, particles, WaitForSeconds); gameplay
    /// input checks IsPaused so clicks and keys do nothing meanwhile.
    ///
    /// HitFeedback's hit stop also uses timeScale, so it asks IsPaused
    /// before restoring it — otherwise a hit stop ending mid-pause would
    /// quietly unfreeze the game.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static void Set(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        // A paused game must never leak into the next scene or the next Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
