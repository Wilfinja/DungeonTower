using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Fills a chest from a theme's chest loot table, scaled to the floor:
    /// only entries whose floor range covers the floor can roll, and the
    /// number of rolls grows with depth into the theme (FloorsPerExtraRoll).
    /// </summary>
    public static class ChestLootRoller
    {
        public static void Fill(Chest chest, DungeonThemeSO theme, int floor, System.Random rng)
        {
            if (chest == null || theme == null || rng == null) return;

            var pool = new List<ThemeLootEntry>();
            foreach (var entry in theme.ChestLoot)
                if (entry.IsValid && entry.AppliesToFloor(floor)) pool.Add(entry);
            if (pool.Count == 0) return;

            int rolls = rng.Next(theme.MinRollsPerChest, theme.MaxRollsPerChest + 1);
            if (theme.FloorsPerExtraRoll > 0)
                rolls += Mathf.Max(0, floor - theme.MinFloor) / theme.FloorsPerExtraRoll;

            for (int i = 0; i < rolls; i++)
            {
                var entry = Pick(pool, rng);
                int count = rng.Next(Mathf.Max(1, entry.MinCount), Mathf.Max(Mathf.Max(1, entry.MinCount), entry.MaxCount) + 1);

                if (entry.Weapon != null) chest.AddWeapon(entry.Weapon);
                else if (entry.Armor != null) chest.AddArmor(entry.Armor);
                else if (entry.Potion != null) chest.AddPotion(entry.Potion, count);
                else if (entry.Scroll != null) chest.AddScroll(entry.Scroll, count);
            }
        }

        private static ThemeLootEntry Pick(List<ThemeLootEntry> pool, System.Random rng)
        {
            double total = 0;
            foreach (var e in pool) total += e.Weight;

            double pick = rng.NextDouble() * total;
            var chosen = pool[pool.Count - 1];
            foreach (var e in pool)
            {
                pick -= e.Weight;
                if (pick < 0) { chosen = e; break; }
            }
            return chosen;
        }
    }
}
