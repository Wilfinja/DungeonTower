using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A reusable enemy "kit" — which class an enemy uses, what role it
    /// plays in a room's composition, its specific weapon/armor, and now
    /// how it picks targets: an offense strategy for which enemy to
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
        public EnemyTargetingStrategy TargetingStrategy => _targetingStrategy;
        public int TargetingRange => _targetingRange;
        public SupportTargetingStrategy SupportTargetingStrategy => _supportTargetingStrategy;
        public float SupportHealThreshold => _supportHealThreshold;
    }
}
