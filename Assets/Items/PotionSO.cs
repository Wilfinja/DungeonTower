using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IConsumable for a simple self/ally-targeted heal.
    /// Leave RequiredStatValue at 0 (the default) so anyone can drink it
    /// with no skill check — set it only if you want certain potions
    /// gated (e.g. an elixir only a high-Body unit can stomach).
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Potion", fileName = "NewPotion")]
    public sealed class PotionSO : ScriptableObject, IPotion
    {
        [SerializeField] private string _potionName;
        [SerializeField] private PotionId _id;
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue;
        [SerializeField, Min(0)] private int _healHp;
        [SerializeField, Min(0)] private int _healMp;

        public string Name => _potionName;
        public PotionId Id => _id;
        public PrimaryStat RequiredStat => _requiredStat;
        public int RequiredStatValue => _requiredStatValue;
        public int HealHp => _healHp;
        public int HealMp => _healMp;

        public bool CanUse(StatBlock stats) => RequiredStatValue <= 0 || stats.Get(RequiredStat) >= RequiredStatValue;
    }
}
