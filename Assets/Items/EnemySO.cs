using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A reusable enemy "kit" — which class/weapon/armor combo an enemy
    /// uses and what role it plays in a room's composition. Doesn't
    /// implement an interface the way Weapon/Armor/Potion/Scroll do,
    /// since nothing constructs a CombatUnit through Items directly —
    /// BattleController reads these plain fields the same way it already
    /// reads a hardcoded ClassId/WeaponId/ArmorId combo today, just from
    /// data instead of from code.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Enemy", fileName = "NewEnemy")]
    public sealed class EnemySO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private EnemyRole _role;
        [SerializeField] private ClassId _classId;
        [SerializeField] private WeaponId _weaponId;
        [SerializeField] private ArmorId _armorId;
        [SerializeField, Min(1)] private int _detectionRadius = 5;
        [SerializeField, Min(1)] private int _alertRadius = 4;

        public string DisplayName => _displayName;
        public EnemyRole Role => _role;
        public ClassId ClassId => _classId;
        public WeaponId WeaponId => _weaponId;
        public ArmorId ArmorId => _armorId;
        public int DetectionRadius => _detectionRadius;
        public int AlertRadius => _alertRadius;
    }
}
