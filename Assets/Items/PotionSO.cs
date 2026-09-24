using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IPotion. Structurally identical to ScrollSO —
    /// both are just "an ability, plus a stat check to use it" — the
    /// difference between them is enforced by BattleController
    /// (potions always resolve on whoever's turn it is; scrolls are
    /// aimed at the grid), not by anything in this class. A potion's
    /// Ability is typically Heal or Buff kind, but nothing here
    /// prevents authoring a Damage-kind one if that's ever wanted.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Potion", fileName = "NewPotion")]
    public sealed class PotionSO : ScriptableObject, IPotion
    {
        [SerializeField] private string _potionName;
        [SerializeField] private PotionId _group;
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue;
        [SerializeField] private AbilitySO _ability;

        public string Name => _potionName;
        public PotionId Group => _group;
        public PrimaryStat RequiredStat => _requiredStat;
        public int RequiredStatValue => _requiredStatValue;
        public IAbility Ability => _ability;

        public bool CanUse(StatBlock stats) => RequiredStatValue <= 0 || stats.Get(RequiredStat) >= RequiredStatValue;
    }
}
