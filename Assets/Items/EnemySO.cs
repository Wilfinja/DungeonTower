using System;
using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A reusable enemy "kit" — its Body/Mind/Spirit, what role it
    /// plays in a room's composition, its specific weapon/armor, the XP
    /// it's worth and the extra potions/scrolls it can drop when it dies,
    /// and how it picks targets: an offense strategy for which enemy to
    /// attack, a support strategy for which ally to help, a shared scan
    /// range both use to find their preferred target before defaulting
    /// to whatever's nearest, and the HP% threshold that decides whether
    /// an ally actually needs healing right now (Cleanse/Buff/beneficial
    /// Status use their own need rules — see SupportNeed — so this
    /// number only matters for a Heal-kind support ability).
    /// Referenced directly (drag the asset in) rather than looked up by
    /// an ID — adding a new weapon variant (Iron Sword, Steel Sword,
    /// ...) never requires touching an enum, only authoring the asset
    /// and dragging it in here. Doesn't implement an interface the way
    /// Weapon/Armor/Potion/Scroll do, since nothing constructs a
    /// CombatUnit through Items directly — BattleController reads these
    /// plain fields the same way it already reads a hardcoded
    /// ClassId/weapon/armor combo today, just from data instead of code.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Enemy", fileName = "NewEnemy")]
    public sealed class EnemySO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private EnemyRole _role;
        [SerializeField, Min(0)] private int _body = 5;
        [SerializeField, Min(0)] private int _mind = 5;
        [SerializeField, Min(0)] private int _spirit = 5;
        [SerializeField] private WeaponSO _weapon;
        [SerializeField] private ArmorSO _armor;
        [SerializeField, Min(1)] private int _detectionRadius = 5;
        [SerializeField, Min(1)] private int _alertRadius = 4;

        [Header("Rewards")]
        [Tooltip("XP earned when this enemy dies. The killer gets it in full, the rest of the living party gets a share (see LevelUpRules).")]
        [SerializeField, Min(0)] private int _xpReward = 10;
        [Tooltip("Percent chance (0-100) that a death drops ONE item from the table below, on top of the weapon/armor it was carrying.")]
        [SerializeField, Range(0f, 100f)] private float _dropChance = 0f;
        [Tooltip("Extra drops: each row is a potion OR a scroll with a relative weight (3 is three times as likely as 1) and a count range.")]
        [SerializeField] private List<EnemyDropEntry> _drops = new List<EnemyDropEntry>();

        [Header("AI Targeting")]
        [Tooltip("Which enemy to attack, among whoever's within Targeting Range. \"Nearest\" is the old default behavior.")]
        [SerializeField] private EnemyTargetingStrategy _targetingStrategy = EnemyTargetingStrategy.Nearest;
        [Tooltip("How far this unit scans for its preferred target (offense) or a needy ally (support) before it just goes with whoever's nearest.")]
        [SerializeField, Min(1)] private int _targetingRange = 8;
        [Tooltip("Which ally to help, among whichever allies within Targeting Range actually need the support ability being considered.")]
        [SerializeField] private SupportTargetingStrategy _supportTargetingStrategy = SupportTargetingStrategy.LowestHpPercent;
        [Tooltip("An ally is worth healing when their HP is at or below this percent of max. Only affects Heal-kind support abilities.")]
        [SerializeField, Range(0, 100)] private float _supportHealThreshold = 50f;

        public string DisplayName => _displayName;
        public EnemyRole Role => _role;
        public StatBlock Stats => new StatBlock(_body, _mind, _spirit);
        public IWeapon Weapon => _weapon;
        public IArmor Armor => _armor;
        public int DetectionRadius => _detectionRadius;
        public int AlertRadius => _alertRadius;
        public int XpReward => _xpReward;
        public float DropChance => _dropChance;

        // Built on demand — only read once, at this enemy's death.
        public IReadOnlyList<DropEntry> Drops
        {
            get
            {
                var result = new List<DropEntry>(_drops != null ? _drops.Count : 0);
                if (_drops != null)
                    foreach (var entry in _drops)
                        if (entry != null && entry.IsValid) result.Add(entry.ToDropEntry());
                return result;
            }
        }
        public EnemyTargetingStrategy TargetingStrategy => _targetingStrategy;
        public int TargetingRange => _targetingRange;
        public SupportTargetingStrategy SupportTargetingStrategy => _supportTargetingStrategy;
        public float SupportHealThreshold => _supportHealThreshold;

        private void OnValidate()
        {
            if (_drops == null) return;
            for (int i = 0; i < _drops.Count; i++)
                if (_drops[i] != null && !_drops[i].IsValid)
                    Debug.LogWarning($"{name}: drop row {i} needs exactly one of Potion/Scroll and a weight above 0 — it will be ignored.", this);
        }
    }

    /// <summary>
    /// One row of an EnemySO's drop table. Fill in exactly one of Potion
    /// or Scroll — a row with both or neither is ignored (and warned about
    /// in the console when the asset is validated).
    /// </summary>
    [Serializable]
    public sealed class EnemyDropEntry
    {
        [SerializeField] private PotionSO _potion;
        [SerializeField] private ScrollSO _scroll;
        [SerializeField, Min(1)] private int _minCount = 1;
        [SerializeField, Min(1)] private int _maxCount = 1;
        [Tooltip("Relative chance against this enemy's other rows — not a percentage.")]
        [SerializeField, Min(0f)] private float _weight = 1f;

        public bool IsValid => (_potion != null) != (_scroll != null) && _weight > 0f;

        public DropEntry ToDropEntry() => new DropEntry(_potion, _scroll, _minCount, _maxCount, _weight);
    }
}
