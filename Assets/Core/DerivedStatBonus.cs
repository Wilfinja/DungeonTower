using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// A flat bonus to one or more derived (secondary) stats — HP, MP,
    /// Physical/Magic Attack and Defense, Initiative, Move Range, Status
    /// Resist, Crit. This is what armor grants. Unlike StatGrowth (which
    /// feeds Body/Mind/Spirit), this never touches the primary stats —
    /// those only ever grow from leveling.
    ///
    /// Plain public fields, not get-only auto-properties, and not a
    /// readonly struct — Unity's Inspector serializer only picks up
    /// mutable public fields (or [SerializeField] private ones); a
    /// get-only-property / readonly-struct version shows as an empty,
    /// un-editable foldout in ArmorSO's Inspector, with nothing to type
    /// numbers into.
    /// </summary>
    [Serializable]
    public struct DerivedStatBonus
    {
        public int Hp;
        public int Mp;
        public float PhysicalAttack;
        public float PhysicalDefense;
        public float MagicAttack;
        public float MagicDefense;
        public int Initiative;
        public int MoveRange;
        public float StatusResist;
        public float CritChance;

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
