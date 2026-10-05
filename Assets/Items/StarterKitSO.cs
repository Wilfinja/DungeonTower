using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// The shopping list for character creation: every weapon, armor,
    /// potion, and scroll a new hero can start with, each with a point
    /// cost, plus the budget every hero gets to spend on them. Add or
    /// remove entries here to change what the creation screen offers —
    /// nothing else needs touching.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Starter Kit", fileName = "StarterKit")]
    public sealed class StarterKitSO : ScriptableObject
    {
        [Serializable] public sealed class WeaponEntry { public WeaponSO Item; [Min(0)] public int Cost = 1; }
        [Serializable] public sealed class ArmorEntry { public ArmorSO Item; [Min(0)] public int Cost = 1; }
        [Serializable] public sealed class PotionEntry { public PotionSO Item; [Min(0)] public int Cost = 1; }
        [Serializable] public sealed class ScrollEntry { public ScrollSO Item; [Min(0)] public int Cost = 1; }

        [Tooltip("Gear points EACH hero can spend across weapon, armor, and belt items.")]
        [SerializeField, Min(0)] private int _budget = 10;

        [SerializeField] private List<WeaponEntry> _weapons = new List<WeaponEntry>();
        [SerializeField] private List<ArmorEntry> _armors = new List<ArmorEntry>();
        [SerializeField] private List<PotionEntry> _potions = new List<PotionEntry>();
        [SerializeField] private List<ScrollEntry> _scrolls = new List<ScrollEntry>();

        public int Budget => _budget;
        public IReadOnlyList<WeaponEntry> Weapons => _weapons;
        public IReadOnlyList<ArmorEntry> Armors => _armors;
        public IReadOnlyList<PotionEntry> Potions => _potions;
        public IReadOnlyList<ScrollEntry> Scrolls => _scrolls;
    }
}
