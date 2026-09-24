using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IWeapon. The single equippable item type carrying
    /// combat behavior: a stat-gated weapon with one or two AbilitySO
    /// references. Combat behavior comes entirely from whatever's
    /// equipped, not from class — a Body-leaning unit wielding a staff
    /// would fight like a mage, if its Mind ever qualified to equip one.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Weapon", fileName = "NewWeapon")]
    public sealed class WeaponSO : ScriptableObject, IWeapon
    {
        [SerializeField] private string _weaponName;
        [SerializeField] private WeaponId _group;
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue = 4;
        [SerializeField] private List<AbilitySO> _abilities = new List<AbilitySO>();
        [SerializeField] private bool _dropsOnDeath = true;

        public string Name => _weaponName;
        public WeaponId Group => _group;
        public PrimaryStat RequiredStat => _requiredStat;
        public int RequiredStatValue => _requiredStatValue;
        public IReadOnlyList<IAbility> Abilities => _abilities;
        public bool DropsOnDeath => _dropsOnDeath;

        public bool CanEquip(StatBlock stats) => stats.Get(RequiredStat) >= RequiredStatValue;

        // Editor-time guard only — mirrors the old constructor's
        // ArgumentException, but a ScriptableObject can't throw at
        // creation time the way the old `new Weapon(...)` could, so this
        // is the closest equivalent: a loud warning in the Inspector/
        // console instead of a hard failure at runtime.
        private void OnValidate()
        {
            if (_abilities.Count == 0 || _abilities.Count > 2)
                Debug.LogWarning(
                    $"{name}: a weapon must carry one or two abilities (currently has {_abilities.Count}).", this);
        }
    }
}
