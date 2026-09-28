namespace DungeonTower.Core
{
    public static class DerivedStatBonusExtensions
    {
        /// <summary>
        /// Multiplies every field by a whole-number factor — used to
        /// scale a per-stack stat modifier by a status's stack count.
        /// </summary>
        public static DerivedStatBonus Scaled(this DerivedStatBonus b, int factor) => new DerivedStatBonus(
            hp: b.Hp * factor, mp: b.Mp * factor,
            physicalAttack: b.PhysicalAttack * factor, physicalDefense: b.PhysicalDefense * factor,
            magicAttack: b.MagicAttack * factor, magicDefense: b.MagicDefense * factor,
            initiative: b.Initiative * factor, moveRange: b.MoveRange * factor,
            statusResist: b.StatusResist * factor, critChance: b.CritChance * factor);
    }
}
