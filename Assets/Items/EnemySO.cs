using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A reusable enemy "kit" — which class an enemy uses, what role it
    /// plays in a room's composition, and its specific weapon/armor,
    /// referenced directly (drag the asset in) rather than looked up by
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
        [SerializeField] private ClassId _classId;
        [SerializeField] private WeaponSO _weapon;
        [SerializeField] private ArmorSO _armor;
        [SerializeField, Min(1)] private int _detectionRadius = 5;
        [SerializeField, Min(1)] private int _alertRadius = 4;

        public string DisplayName => _displayName;
        public EnemyRole Role => _role;
        public ClassId ClassId => _classId;
        public IWeapon Weapon => _weapon;
        public IArmor Armor => _armor;
        public int DetectionRadius => _detectionRadius;
        public int AlertRadius => _alertRadius;
    }
}
