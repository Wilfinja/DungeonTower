using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Generic weighted-random pick from a list of (item, weight) pairs.
    /// Used for both role-slot selection (minion much likelier than
    /// boss) and picking a specific EnemySO within a role — same
    /// algorithm, different data. A non-positive total weight falls
    /// back to the first option rather than throwing, so a
    /// mis-authored all-zero-weight list degrades instead of crashing.
    /// </summary>
    public static class WeightedRandom
    {
        public static T Pick<T>(IReadOnlyList<(T Item, float Weight)> options, System.Random rng)
        {
            float total = 0f;
            foreach (var (_, weight) in options)
                total += System.Math.Max(0f, weight);

            if (total <= 0f)
                return options.Count > 0 ? options[0].Item : default;

            float roll = (float)(rng.NextDouble() * total);
            float cumulative = 0f;
            foreach (var (item, weight) in options)
            {
                cumulative += System.Math.Max(0f, weight);
                if (roll < cumulative) return item;
            }

            return options[options.Count - 1].Item; // floating-point edge case fallback
        }
    }
}
