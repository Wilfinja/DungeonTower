namespace DungeonTower.Core
{
    /// <summary>
    /// A flat bonus to one or more derived (secondary) stats — HP, MP,
    /// Physical/Magic Attack and Defense, Initiative, Move Range, Status
    /// Resist, Crit. This is what armor grants. Unlike StatGrowth (which
    /// feeds Body/Mind/Spirit), this never touches the primary stats —
    /// those only ever grow from leveling.
    /// </summary>
    public readonly struct DerivedStatBonus
    {
        public int Hp { get; }
        public int Mp { get; }
        public float PhysicalAttack { get; }
        public float PhysicalDefense { get; }
        public float MagicAttack { get; }
        public float MagicDefense { get; }
        public int Initiative { get; }
        public int MoveRange { get; }
        public float StatusResist { get; }
        public float CritChance { get; }

        public DerivedStatBonus(
            int hp = 0, int mp = 0,
            float physicalAttack = 0, float physicalDefense = 0,
            float magicAttack = 0, float magicDefense = 0,
            int initiative = 0, int moveRange = 0,
            float statusResist = 0, float critChance = 0)
        {
            Hp = hp;
            Mp = mp;
            PhysicalAttack = physicalAttack;
            PhysicalDefense = physicalDefense;
            MagicAttack = magicAttack;
            MagicDefense = magicDefense;
            Initiative = initiative;
            MoveRange = moveRange;
            StatusResist = statusResist;
            CritChance = critChance;
        }
    }
}
