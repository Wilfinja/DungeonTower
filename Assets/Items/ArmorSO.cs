using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Asset form of IArmor. The second and last equip slot: a stat-gated
    /// passive that boosts derived stats (HP, defenses, etc.) — never
    /// Body/Mind/Spirit directly, since those only grow from leveling.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Tower/Armor", fileName = "NewArmor")]
    public sealed class ArmorSO : ScriptableObject, IArmor
    {
        [SerializeField] private string _armorName;
        [SerializeField] private ArmorId _id;
        [SerializeField] private PrimaryStat _requiredStat;
        [SerializeField, Min(0)] private int _requiredStatValue = 4;
        [SerializeField] private DerivedStatBonus _passiveBonus;

        public string Name => _armorName;
        public ArmorId Id => _id;
        public PrimaryStat RequiredStat => _requiredStat;
        public int RequiredStatValue => _requiredStatValue;
        public DerivedStatBonus PassiveBonus => _passiveBonus;

        public bool CanEquip(StatBlock stats) => stats.Get(RequiredStat) >= RequiredStatValue;
    }
}
