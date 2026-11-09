using System;
using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    // Plain mutable structs with public fields (not get-only properties)
    // so Unity's Inspector can actually serialize them in a list — see
    // DerivedStatBonus for why get-only properties silently fail to show
    // up here.
    [Serializable]
    public struct ThemeEnemyEntry
    {
        public EnemySO Enemy;
        [Min(0.01f)] public float Weight;
    }

    [Serializable]
    public struct RoleWeightEntry
    {
        public EnemyRole Role;
        [Min(0f)] public float Weight;
    }

    /// <summary>
    /// One possible chest item for a theme: exactly one of Weapon / Armor /
    /// Potion / Scroll, how likely it is relative to the other entries, and
    /// the floors it can appear on. MinFloor/MaxFloor are inclusive (0 = no
    /// limit), so a theme can keep its best gear for its deeper floors.
    /// Count only applies to potions and scrolls.
    /// </summary>
    [Serializable]
    public struct ThemeLootEntry
    {
        public WeaponSO Weapon;
        public ArmorSO Armor;
        public PotionSO Potion;
        public ScrollSO Scroll;
        [Min(0.01f)] public float Weight;
        [Min(0)] public int MinFloor;
        [Min(0)] public int MaxFloor;
        [Min(1)] public int MinCount;
        [Min(1)] public int MaxCount;

        public bool IsValid
        {
            get
            {
                int items = (Weapon != null ? 1 : 0) + (Armor != null ? 1 : 0)
                    + (Potion != null ? 1 : 0) + (Scroll != null ? 1 : 0);
                return items == 1 && Weight > 0f;
            }
        }

        public bool AppliesToFloor(int floor)
            => (MinFloor <= 0 || floor >= MinFloor) && (MaxFloor <= 0 || floor <= MaxFloor);
    }

    /// <summary>
    /// One dungeon theme's enemy pool — which floors it applies to,
    /// which EnemySOs can appear (each with a relative spawn weight
    /// within this theme — the same EnemySO can be reused across themes
    /// with a different weight in each), how likely each ROLE is to
    /// fill a slot before an enemy is even chosen (RoleWeights — this is
    /// the "minion way more likely than boss" table), and how many
    /// Boss-role enemies are allowed on one floor of this theme.
    /// MinFloor/MaxFloor are inclusive; ThemeRegistry picks whichever
    /// theme's range contains the floor currently being built.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Dungeon Theme", fileName = "NewDungeonTheme")]
    public sealed class DungeonThemeSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private int _minFloor = 1;
        [SerializeField] private int _maxFloor = 5;
        [SerializeField] private List<ThemeEnemyEntry> _enemies = new List<ThemeEnemyEntry>();
        [SerializeField] private List<RoleWeightEntry> _roleWeights = new List<RoleWeightEntry>();
        [SerializeField, Min(0)] private int _maxBossesPerFloor = 1;

        [Header("Chests")]
        [Tooltip("How many chests appear on each floor of this theme (a random number from Min to Max). 0 and 0 = no chests.")]
        [SerializeField, Min(0)] private int _minChestsPerFloor = 1;
        [SerializeField, Min(0)] private int _maxChestsPerFloor = 3;
        [Tooltip("How many times each chest rolls the loot table below (random from Min to Max).")]
        [SerializeField, Min(1)] private int _minRollsPerChest = 1;
        [SerializeField, Min(1)] private int _maxRollsPerChest = 2;
        [Tooltip("Every this-many floors into the theme, chests get one more roll. 0 = never. This is how loot keeps up with depth inside a theme.")]
        [SerializeField, Min(0)] private int _floorsPerExtraRoll = 0;
        [Tooltip("What chests in this theme can hold. Give each entry a Floor range to unlock better items deeper in.")]
        [SerializeField] private List<ThemeLootEntry> _chestLoot = new List<ThemeLootEntry>();

        public string DisplayName => _displayName;
        public int MinFloor => _minFloor;
        public int MaxFloor => _maxFloor;
        public IReadOnlyList<ThemeEnemyEntry> Enemies => _enemies;
        public IReadOnlyList<RoleWeightEntry> RoleWeights => _roleWeights;
        public int MaxBossesPerFloor => _maxBossesPerFloor;
        public int MinChestsPerFloor => _minChestsPerFloor;
        public int MaxChestsPerFloor => Mathf.Max(_minChestsPerFloor, _maxChestsPerFloor);
        public int MinRollsPerChest => _minRollsPerChest;
        public int MaxRollsPerChest => Mathf.Max(_minRollsPerChest, _maxRollsPerChest);
        public int FloorsPerExtraRoll => _floorsPerExtraRoll;
        public IReadOnlyList<ThemeLootEntry> ChestLoot => _chestLoot;

        public bool CoversFloor(int floor) => floor >= _minFloor && floor <= _maxFloor;
    }
}
