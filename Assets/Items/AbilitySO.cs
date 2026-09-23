using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IAbility. One thing a weapon (or scroll) lets its
    /// wielder do in a fight — an attack kind, a range, a damage
    /// multiplier applied on top of the wielder's raw Physical or Magic
    /// Attack, and an area footprint. Leave AreaShape at Single (the
    /// default) for a normal single-target ability; only set AreaRadius
    /// when AreaShape is Blast/Line/Cone. A WeaponSO holds one or two of
    /// these as direct asset references; a ScrollSO holds exactly one.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Ability", fileName = "NewAbility")]
    public sealed class AbilitySO : ScriptableObject, IAbility
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private AttackKind _kind;
        [SerializeField, Min(1)] private int _range = 1;
        [SerializeField, Min(0f)] private float _damageMultiplier = 1.0f;
        [SerializeField] private AttackShape _areaShape = AttackShape.Single;
        [SerializeField, Min(0)] private int _areaRadius;

        public string Name => _abilityName;
        public AttackKind Kind => _kind;
        public int Range => _range;
        public float DamageMultiplier => _damageMultiplier;
        public AttackShape AreaShape => _areaShape;
        public int AreaRadius => _areaRadius;
    }
}
