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
        [Tooltip("One-handed: either hand (two = dual wield). Off-hand: shields/focuses, off hand only. Two-handed: fills both hands, up to 3 abilities.")]
        [SerializeField] private HandUse _hands = HandUse.OneHanded;
        [Tooltip("Every entry must be met. Leave EMPTY to use the single legacy requirement below.")]
        [SerializeField] private List<StatRequirement> _requirements = new List<StatRequirement>();
        [Tooltip("Legacy single requirement - only used while the list above is empty.")]
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue = 4;
        [Tooltip("Flat bonuses while equipped (negative allowed: a heavy weapon can cost Initiative).")]
        [SerializeField] private DerivedStatBonus _passiveBonus;
        [SerializeField] private List<AbilitySO> _abilities = new List<AbilitySO>();
        [SerializeField] private bool _dropsOnDeath = true;

        public string Name => _weaponName;
        public WeaponId Group => _group;
        public HandUse Hands => _hands;
        public DerivedStatBonus PassiveBonus => _passiveBonus;

        private StatRequirement[] _legacyRequirement;

        public IReadOnlyList<StatRequirement> Requirements
        {
            get
            {
                if (_requirements != null && _requirements.Count > 0) return _requirements;
                if (_legacyRequirement == null)
                    _legacyRequirement = new[] { new StatRequirement(_requiredStat, _requiredStatValue) };
                return _legacyRequirement;
            }
        }

        public PrimaryStat RequiredStat => Requirements[0].Stat;
        public int RequiredStatValue => Requirements[0].Value;
        public IReadOnlyList<IAbility> Abilities => _abilities;
        public bool DropsOnDeath => _dropsOnDeath;

        public bool CanEquip(StatBlock stats) => HandRules.MeetsRequirements(this, stats);

        // Editor-time guard only — mirrors the old constructor's
        // ArgumentException, but a ScriptableObject can't throw at
        // creation time the way the old `new Weapon(...)` could, so this
        // is the closest equivalent: a loud warning in the Inspector/
        // console instead of a hard failure at runtime.
        private void OnValidate()
        {
            _legacyRequirement = null;

            int max = HandRules.MaxAbilitiesFor(_hands);
            if (_abilities.Count == 0 || _abilities.Count > max)
                Debug.LogWarning(
                    $"{name}: a {HandRules.Describe(_hands)} weapon must carry 1 to {max} abilities (currently has {_abilities.Count}).", this);
        }
    }
}
