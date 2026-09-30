using System;
using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// One row of an enemy's consumable drop table: exactly one of
    /// Potion/Scroll, how many drop (a random count between MinCount and
    /// MaxCount, inclusive), and a relative Weight against the other rows.
    /// </summary>
    public readonly struct DropEntry
    {
        public IPotion Potion { get; }
        public IScroll Scroll { get; }
        public int MinCount { get; }
        public int MaxCount { get; }
        public float Weight { get; }

        public DropEntry(IPotion potion, IScroll scroll, int minCount, int maxCount, float weight)
        {
            Potion = potion;
            Scroll = scroll;
            MinCount = Math.Max(1, minCount);
            MaxCount = Math.Max(MinCount, maxCount);
            Weight = weight;
        }

        // A row needs exactly one item to be usable; the roller skips
        // anything else (and EnemySO warns about it in the editor).
        public bool IsValid => (Potion != null) != (Scroll != null) && Weight > 0f;
    }

    public readonly struct DropResult
    {
        public IPotion Potion { get; }
        public IScroll Scroll { get; }
        public int Count { get; }

        public DropResult(IPotion potion, IScroll scroll, int count)
        {
            Potion = potion;
            Scroll = scroll;
            Count = count;
        }
    }

    /// <summary>
    /// The roll behind an enemy's extra drops: first a flat percent
    /// chance that ANYTHING drops, then ONE weighted pick from the table
    /// (so a death never drops more than one kind of consumable), then a
    /// random count for the winner. Pure logic with an injected RNG so
    /// it's testable without a scene.
    /// </summary>
    public static class DropTable
    {
        public static bool TryRoll(IReadOnlyList<DropEntry> entries, float chancePercent, Random rng, out DropResult result)
        {
            result = default;
            if (entries == null || entries.Count == 0 || rng == null || chancePercent <= 0f) return false;
            if (chancePercent < 100f && rng.NextDouble() * 100.0 >= chancePercent) return false;

            double totalWeight = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].IsValid) totalWeight += entries[i].Weight;
            if (totalWeight <= 0) return false;

            double pick = rng.NextDouble() * totalWeight;
            int chosen = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsValid) continue;
                chosen = i; // remembers the last valid row in case rounding lets `pick` run past the end
                pick -= entries[i].Weight;
                if (pick < 0) break;
            }
            if (chosen < 0) return false;

            var entry = entries[chosen];
            int count = rng.Next(entry.MinCount, entry.MaxCount + 1);
            result = new DropResult(entry.Potion, entry.Scroll, count);
            return true;
        }
    }
}
