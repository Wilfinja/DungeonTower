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
        [SerializeField] private int passiveRange;
        [Tooltip("How many added squares an enemy is pushed when using a push ability.")]
        [SerializeField] private int passivePushRange;
        [Tooltip("How many added squares an enemy is pulled when using a push ability.")]
        [SerializeField] private int passivePullRange;




        public string Name => passiveName;

        
        public int passivehpRegen => hpRegen;
        public int passivempRegen => mpRegen;

    }
}
