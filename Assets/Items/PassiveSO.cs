using System;
using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{

    [CreateAssetMenu(menuName = "Dungeon Tower/ArmorPassive", fileName = "ArmorPassive")]
    public sealed class PassiveSO : ScriptableObject, IPassive
    {
        [SerializeField] private string passiveName;

        [Header("Regenerative")]
        [SerializeField] private int mpRegen;
        [SerializeField] private int hpRegen;


        [Header("Ability Enhancers")]
        [Tooltip("Added range to any ranged abilities.")]
        [SerializeField] private int rangeBonus;
        [Tooltip("How many added squares an enemy is pushed when using a push ability.")]
        [SerializeField] private int pushRange;
        [Tooltip("How many added squares an enemy is pulled when using a push ability.")]
        [SerializeField] private int pullRange;
        [Tooltip("Which group of weapon that will be enhanced")]
        [SerializeField] private List<WeaponDamageBonus> weaponDamageBonus = new List<WeaponDamageBonus>();


        [Header("Defenseive")]
        [Tooltip("% of damage reflected on a melee attacker")]
        [SerializeField] private float thornsPercent;
        [Tooltip("Extra tiles beyond adjacent that thorns can reach.")]
        [SerializeField, Min(0)] private int thornsReach;
        [Tooltip("The amount of squares that are resisted from being pushed")]
        [SerializeField] private int pushResist;
        [Tooltip("The amount of squares that are resisted from being pulled")]
        [SerializeField] private int pullResist;
        [Tooltip("Immunities to any type of status effect")]
        [SerializeField] private List<StatusEffectId> statusImmunity = new List<StatusEffectId>();


        [Header("Aura")]
        [Tooltip("The type of damage dealt through the aura)")]
        [SerializeField] private AttackKind _kind;
        [Tooltip("The amount of damage dealt")]
        [SerializeField] private int auraDamage;
        [Tooltip("The range of the aura that deals damage")]
        [SerializeField] private int auraRange;
        [Tooltip("The amount of heal given to allies but not to self")]
        [SerializeField] private int auraHeal;
        [Tooltip("The range of the aura that heals allies")]
        [SerializeField, Min(0)] private int auraHealRange;

        [Header("On Kill")]
        [SerializeField] private int onKillHeal;
        [SerializeField] private int onKillMp;
        [Tooltip("Applied to the wearer when they kill an enemy.")]
        [SerializeField] private List<StatusApplication> onKillStatuses = new List<StatusApplication>();

        [Header("Last Stand")]
        [Tooltip("Active at or below this % of max HP. 0 = off.")]
        [SerializeField, Range(0f, 100f)] private float lastStandThreshold;
        [SerializeField, Range(0f, 90f)] private float lastStandReduction;
        [SerializeField, Min(0f)] private float lastStandBonus;


        public string Name => passiveName;

        
        public int passivehpRegen => hpRegen;
        public int passivempRegen => mpRegen;

        public int passiveRangeBonus => rangeBonus;
        public int passivePushRange => pushRange;
        public int passivePullRange => pullRange;
        public IReadOnlyList<WeaponDamageBonus> passiveWeaponDamage => weaponDamageBonus;

        public float passiveThornsPercent => thornsPercent;
        public int passivePushResist => pushResist;
        public int passivePullResist => pullResist;
        public IReadOnlyList<StatusEffectId> passiveImmunity => statusImmunity;

        public AttackKind passiveKind => _kind;
        public int passiveAuraDamage => auraDamage;
        public int passiveAuraRange => auraRange;

        public int passiveThornsReach => thornsReach;
        public int passiveAuraHeal => auraHeal;
        public int passiveAuraHealRange => auraHealRange;
        public int passiveOnKillHeal => onKillHeal;
        public int passiveOnKillMp => onKillMp;
        public IReadOnlyList<StatusApplication> passiveOnKillStatuses => onKillStatuses;
        public float passiveLastStandThreshold => lastStandThreshold;
        public float passiveLastStandReduction => lastStandReduction;
        public float passiveLastStandBonus => lastStandBonus;


    }
}
