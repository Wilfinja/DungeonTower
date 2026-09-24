using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IConsumable for a one-shot cast: carries an
    /// AbilitySO reference so an offensive scroll (e.g. Fireball) behaves
    /// exactly like the matching weapon ability would — same Kind,
    /// Range, DamageMultiplier, AreaShape, AreaRadius — without needing
    /// the weapon equipped. RequiredStat is the check to read the scroll
    /// itself (typically Mind), separate from the ability's own numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Scroll", fileName = "NewScroll")]
    public sealed class ScrollSO : ScriptableObject, IScroll
    {
        [SerializeField] private string _scrollName;
        [SerializeField] private ScrollId _group;
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue;
        [SerializeField] private AbilitySO _ability;

        public string Name => _scrollName;
        public ScrollId Group => _group;
        public PrimaryStat RequiredStat => _requiredStat;
        public int RequiredStatValue => _requiredStatValue;
        public IAbility Ability => _ability;

        public bool CanUse(StatBlock stats) => RequiredStatValue <= 0 || stats.Get(RequiredStat) >= RequiredStatValue;
    }
}
