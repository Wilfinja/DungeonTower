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
        [Tooltip("The amount of squares that are resisted from being pushed")]
        [SerializeField] private int pushResist;
        [Tooltip("The amount of squares that are resisted from being pulled")]
        [SerializeField] private int pullResist;
        [Tooltip("Immunities to any type of status effect")]
        [SerializeField] private List<StatusEffectId> statusImmunity = new List<StatusEffectId>();


        [Header("Aura Damage")]
        [Tooltip("The type of damage dealt through the aura)")]
        [SerializeField] private AttackKind _kind;
        [Tooltip("The amount of damage dealt")]
        [SerializeField] private int auraDamage;
        [Tooltip("The range of the aura")]
        [SerializeField] private int auraRange;


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




    }
}
