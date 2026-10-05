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

        [Header("Silence")]
        [Tooltip("If ticked, a Silenced unit can't use this from an equipped weapon. Scrolls are always blocked by Silence and potions never are, regardless of this box.")]
        [SerializeField] private bool _silenceable;

        [Header("Cleanse (EffectKind = Cleanse) — leave empty to remove every harmful status")]
        [SerializeField] private List<StatusEffectId> _cleanses = new List<StatusEffectId>();

        [Header("Push / Pull / Swap (any EffectKind)")]
        [Tooltip("Tiles to knock the target away from the attacker; negative pulls it toward the attacker instead. 0 = none.")]
        [SerializeField] private int _pushDistance;
        [Tooltip("Instantly swaps the attacker's and target's positions. Don't combine with a nonzero Push Distance.")]
        [SerializeField] private bool _swapWithCaster;

        [Header("Summon (EffectKind = Summon) — footprint comes from AreaShape/AreaRadius above")]
        [SerializeField] private SummonTriggerMode _summonTriggerMode = SummonTriggerMode.None;
        [Tooltip("Reach beyond the footprint that OnRoundTick still affects. Unused by OnEntry/None.")]
        [SerializeField, Min(0)] private int _auraRadius;
        [Tooltip("Who Statuses apply to, relative to the owner: allies (a beneficial totem/aura) or everyone else (hostile).")]
        [SerializeField] private bool _summonAffectsAllies;
        [Tooltip("OnEntry only: skip the owner's own faction (the usual case for a trap).")]
        [SerializeField] private bool _summonIgnoreOwnerFaction = true;
        [Tooltip("OnEntry only: remove after firing once (Trap) vs. keep firing on every entry (a hazard zone).")]
        [SerializeField] private bool _summonConsumedAfterTrigger = true;
        [Tooltip("No visible marker until it triggers.")]
        [SerializeField] private bool _summonIsHidden;
        [Tooltip("Rounds before this expires on its own. 0 or less = no duration cap.")]
        [SerializeField] private int _summonDuration;
        [Tooltip("HP before this is destroyed by damage. 0 or less = indestructible.")]
        [SerializeField] private int _summonMaxHp;
        [Tooltip("Tied to the caster — removed the instant they move, die, or Summon Duration runs out (Aura). Off for an independent object (Totem/Trap/Wall/Cloud).")]
        [SerializeField] private bool _endsIfOwnerMoves;
        [Tooltip("Blocks movement onto the footprint (impassable terrain / a solid Wall).")]
        [SerializeField] private bool _summonBlocksMovement;
        [Tooltip("Blocks line of sight through the footprint without blocking movement (an obscuring cloud; also usually on for a solid Wall).")]
        [SerializeField] private bool _summonBlocksLineOfSight;

        [Header("Targeting")]
        [SerializeField, Min(1)] private int _range = 1;
        [SerializeField] private AttackShape _areaShape = AttackShape.Single;
        [SerializeField, Min(0)] private int _areaRadius;
        [SerializeField, Min(0)] private int _cooldownTurns;

        [Header("Cost")]
        [Tooltip("MP spent each time this is used from a weapon or read from a scroll. 0 = free. Potions never charge MP, whatever this says.")]
        [SerializeField, Min(0)] private int _mpCost;

        [Header("Visuals")]
        [SerializeField] private AbilityVisuals _visuals = new AbilityVisuals();
        public AbilityVisuals Visuals => _visuals;

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
        public int MpCost => _mpCost;
        public bool Silenceable => _silenceable;

        // Never null, even for an asset serialized before these fields existed.
        public IReadOnlyList<StatusApplication> Statuses
            => _statuses ?? (IReadOnlyList<StatusApplication>)Array.Empty<StatusApplication>();
        public IReadOnlyList<StatusEffectId> Cleanses
            => _cleanses ?? (IReadOnlyList<StatusEffectId>)Array.Empty<StatusEffectId>();

        public int PushDistance => _pushDistance;
        public bool SwapWithCaster => _swapWithCaster;

        public SummonTriggerMode SummonTriggerMode => _summonTriggerMode;
        public int AuraRadius => _auraRadius;
        public bool SummonAffectsAllies => _summonAffectsAllies;
        public bool SummonIgnoreOwnerFaction => _summonIgnoreOwnerFaction;
        public bool SummonConsumedAfterTrigger => _summonConsumedAfterTrigger;
        public bool SummonIsHidden => _summonIsHidden;
        public int SummonDuration => _summonDuration;
        public int SummonMaxHp => _summonMaxHp;
        public bool EndsIfOwnerMoves => _endsIfOwnerMoves;
        public bool SummonBlocksMovement => _summonBlocksMovement;
        public bool SummonBlocksLineOfSight => _summonBlocksLineOfSight;
    }
}
