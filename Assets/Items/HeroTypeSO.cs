using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A kind of hero you can recruit at the tavern: a Warrior, a Barbarian
    /// and so on. Heroes are types, not named characters: every Warrior
    /// starts with the same stats and the same starting gear. Make one per
    /// type (Create > Dungeon Tower > Hero Type) and add it to the Hero
    /// Type Registry.
    ///
    /// Starting stats are absolute (a Warrior might be 6 Body, 3 Mind,
    /// 3 Spirit); there are no creation points to spend any more. Level-ups
    /// still give points the player spends as before.
    ///
    /// The asset's file name is its ID in the town save, so don't rename
    /// one that players have already recruited.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Hero Type", fileName = "NewHeroType")]
    public sealed class HeroTypeSO : ScriptableObject
    {
        [SerializeField] private string _displayName = "Warrior";
        [SerializeField, TextArea] private string _description;

        [Header("Starting stats")]
        [SerializeField, Min(0)] private int _body = 3;
        [SerializeField, Min(0)] private int _mind = 3;
        [SerializeField, Min(0)] private int _spirit = 3;

        [Header("Tavern")]
        [Tooltip("Available from the very first visit, no gems needed.")]
        [SerializeField] private bool _startsUnlocked;
        [Tooltip("Gems to recruit this type. Ignored if it starts unlocked.")]
        [SerializeField, Min(0)] private int _gemCost = 20;

        [Header("Starting gear")]
        [SerializeField] private WeaponSO _weapon;
        [Tooltip("Optional: shield, focus, or a second one-handed weapon.")]
        [SerializeField] private WeaponSO _offHand;
        [SerializeField] private ArmorSO _armor;
        [Tooltip("Fills belt slots in order; repeat an asset to stack it.")]
        [SerializeField] private List<PotionSO> _beltPotions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _beltScrolls = new List<ScrollSO>();

        /// <summary>The key for this type in the town save.</summary>
        public string Id => name;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public StatBlock Stats => new StatBlock(_body, _mind, _spirit);
        public bool StartsUnlocked => _startsUnlocked;
        public int GemCost => _gemCost;

        public WeaponSO Weapon => _weapon;
        public WeaponSO OffHand => _offHand;
        public ArmorSO Armor => _armor;
        public IReadOnlyList<PotionSO> BeltPotions => _beltPotions;
        public IReadOnlyList<ScrollSO> BeltScrolls => _beltScrolls;

        /// <summary>Everything this type starts with, for the save system's item lookup.</summary>
        public IEnumerable<Object> GearAssets()
        {
            yield return _weapon;
            yield return _offHand;
            yield return _armor;
            foreach (var potion in _beltPotions) yield return potion;
            foreach (var scroll in _beltScrolls) yield return scroll;
        }

        /// <summary>This type as the game builds a hero from it.</summary>
        public HeroSetup ToSetup()
        {
            var setup = new HeroSetup
            {
                Name = DisplayName,
                BaseStats = Stats,
                Weapon = _weapon,
                OffHand = _offHand,
                Armor = _armor
            };
            foreach (var potion in _beltPotions) if (potion != null) setup.BeltPotions.Add(potion);
            foreach (var scroll in _beltScrolls) if (scroll != null) setup.BeltScrolls.Add(scroll);
            return setup;
        }
    }
}
