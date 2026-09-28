using System;
using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IAbility. EffectKind picks which of the field
    /// groups below actually matters — leave the others at their
    /// defaults, they're simply ignored. A WeaponSO holds one or two of
    /// these as direct asset references; a ScrollSO or PotionSO holds
    /// exactly one — the same asset type covers all three, so a "Heal"
    /// ability could equally be a staff attack, a scroll cast on an
    /// ally, or a potion's effect.
    ///
    /// The Statuses list works on every kind: on EffectKind.Status it IS
    /// the ability; on Damage/Heal/Buff it's a set of on-hit procs. Old
    /// assets deserialize with an empty list and behave exactly as before.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Ability", fileName = "NewAbility")]
    public sealed class AbilitySO : ScriptableObject, IAbility
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private EffectKind _effectKind = EffectKind.Damage;

        [Header("Damage (EffectKind = Damage)")]
        [SerializeField] private AttackKind _kind;
        [SerializeField, Min(0f)] private float _damageMultiplier = 1.0f;

        [Header("Heal (EffectKind = Heal)")]
        [SerializeField, Min(0)] private int _healHp;
        [SerializeField, Min(0)] private int _healMp;

        [Header("Buff (EffectKind = Buff) — permanent for the battle")]
        [SerializeField] private DerivedStatBonus _bonus;

        [Header("Statuses (the payload for EffectKind = Status; on-hit procs for any other kind)")]
        [SerializeField] private List<StatusApplication> _statuses = new List<StatusApplication>();

        [Header("Targeting")]
        [SerializeField, Min(1)] private int _range = 1;
        [SerializeField] private AttackShape _areaShape = AttackShape.Single;
        [SerializeField, Min(0)] private int _areaRadius;
        [SerializeField, Min(0)] private int _cooldownTurns;

        public string Name => _abilityName;
        public EffectKind EffectKind => _effectKind;
        public AttackKind Kind => _kind;
        public float DamageMultiplier => _damageMultiplier;
        public int HealHp => _healHp;
        public int HealMp => _healMp;
        public DerivedStatBonus Bonus => _bonus;
        public int Range => _range;
        public AttackShape AreaShape => _areaShape;
        public int AreaRadius => _areaRadius;
        public int CooldownTurns => _cooldownTurns;

        // Never null, even for an asset serialized before this field existed.
        public IReadOnlyList<StatusApplication> Statuses
            => _statuses ?? (IReadOnlyList<StatusApplication>)Array.Empty<StatusApplication>();
    }
}
