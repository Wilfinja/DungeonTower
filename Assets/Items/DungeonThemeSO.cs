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

        public string DisplayName => _displayName;
        public int MinFloor => _minFloor;
        public int MaxFloor => _maxFloor;
        public IReadOnlyList<ThemeEnemyEntry> Enemies => _enemies;
        public IReadOnlyList<RoleWeightEntry> RoleWeights => _roleWeights;
        public int MaxBossesPerFloor => _maxBossesPerFloor;

        public bool CoversFloor(int floor) => floor >= _minFloor && floor <= _maxFloor;
    }
}
