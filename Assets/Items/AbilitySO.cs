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

        [Header("Scaling - what powers this ability (Damage attack power / Heal bonus)")]
        [Tooltip("Each entry adds Weight x that stat's power: Body = Physical Attack, Mind = Magic Attack, Spirit = Support Power. Guide: a pure one-stat ability totals about 1.0, a hybrid spread over two or more stats about 1.2. Leave EMPTY to use the old Kind + Damage Multiplier (damage) or Heal Support Multiplier (heal) instead.")]
        [SerializeField] private List<StatWeight> _statWeights = new List<StatWeight>();
        [Tooltip("Calculated from the list above - anything typed here is overwritten.")]
        [SerializeField, AutoField] private string _scalingSummary;

        [Header("Damage (EffectKind = Damage)")]
        [Tooltip("Which DEFENSE this hits: Physical = Physical Defense, Magical = Magic Defense. With an empty Stat Weights list it also chooses the stat (Physical = Body, Magical = Mind).")]
        [SerializeField] private AttackKind _kind;
        [Tooltip("LEGACY: only used while Stat Weights is empty. Tip: right-click this asset's component header > 'Convert legacy fields to Stat Weights'.")]
        [SerializeField, Min(0f)] private float _damageMultiplier = 1.0f;
        [Tooltip("Extra crit chance in percent while using this ability. A dagger might be 15, a greatsword 0. Gear crit adds on top.")]
        [SerializeField, Range(0f, 100f)] private float _critChance = 5f;

        [Header("Heal (EffectKind = Heal)")]
        [SerializeField, Min(0)] private int _healHp;
        [SerializeField, Min(0)] private int _healMp;
        [Tooltip("LEGACY: Support Power x this is added to Heal HP, but only while Stat Weights is empty. 0 = flat heal, which is what potions should use. Prefer a Spirit entry in Stat Weights.")]
        [SerializeField, Min(0f)] private float _healSupportMultiplier;

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
        public float CritChance => _critChance;
        public float HealSupportMultiplier => _healSupportMultiplier;

        // The authored weights, or - when none are authored - the single entry
        // the legacy fields imply, so old assets keep behaving exactly as before.
        public IReadOnlyList<StatWeight> Scaling
            => _statWeights != null && _statWeights.Count > 0 ? _statWeights : LegacyScaling();
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

        // ---- scaling helpers --------------------------------------------------

        [NonSerialized] private StatWeight[] _legacyScaling;

        private IReadOnlyList<StatWeight> LegacyScaling()
        {
            if (_legacyScaling != null) return _legacyScaling;

            switch (_effectKind)
            {
                case EffectKind.Damage:
                    _legacyScaling = _damageMultiplier > 0f
                        ? new[] { new StatWeight(_kind == AttackKind.Physical ? PrimaryStat.Body : PrimaryStat.Mind, _damageMultiplier) }
                        : Array.Empty<StatWeight>();
                    break;

                case EffectKind.Heal:
                    _legacyScaling = _healSupportMultiplier > 0f
                        ? new[] { new StatWeight(PrimaryStat.Spirit, _healSupportMultiplier) }
                        : Array.Empty<StatWeight>();
                    break;

                default:
                    _legacyScaling = Array.Empty<StatWeight>();
                    break;
            }
            return _legacyScaling;
        }

        private void OnValidate()
        {
            _legacyScaling = null;

            if (_statWeights == null) _statWeights = new List<StatWeight>();
            for (int i = 0; i < _statWeights.Count; i++)
            {
                var w = _statWeights[i];
                if (w.Weight < 0f)
                {
                    w.Weight = 0f;
                    _statWeights[i] = w;
                }
            }

            string summary = AbilityScaling.Summarize(this);
            if (_statWeights.Count == 0 && AbilityScaling.HasScaling(this)) summary += "   [from legacy fields]";
            _scalingSummary = summary;
        }

        [ContextMenu("Convert legacy fields to Stat Weights")]
        private void ConvertLegacyToWeights()
        {
            if (_statWeights.Count > 0)
            {
                Debug.Log($"{name}: already uses Stat Weights - nothing to convert.", this);
                return;
            }

            var legacy = LegacyScaling();
            if (legacy.Count == 0)
            {
                Debug.Log($"{name}: no legacy scaling to convert.", this);
                return;
            }

            foreach (var w in legacy) _statWeights.Add(w);
            OnValidate();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"{name}: converted to Stat Weights ({_scalingSummary}).", this);
        }
    }
}
