using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// Runtime stat state for one unit: its class, level, accumulated
    /// Body/Mind/Spirit, and equipped armor. Growth accumulates as float
    /// internally so a small secondary-stat growth per level doesn't round
    /// away to nothing — only the exposed stat values round.
    ///
    /// Primary stats (Body/Mind/Spirit, and Current below) only ever grow
    /// from leveling. Armor never touches them — it adds a flat bonus
    /// directly onto the derived stats instead, applied on top of the
    /// formula result in each property below.
    /// </summary>
    public sealed class UnitStats
    {
        private float _body;
        private float _mind;
        private float _spirit;

        public ClassDefinition Class { get; }
        public int Level { get; private set; }
        public IArmor EquippedArmor { get; private set; }

        public int Body => RoundToInt(_body);
        public int Mind => RoundToInt(_mind);
        public int Spirit => RoundToInt(_spirit);

        public StatBlock Current => new StatBlock(Body, Mind, Spirit);

        private DerivedStatBonus ArmorBonus => EquippedArmor?.PassiveBonus ?? default;

        public int MaxHp => DerivedStatFormulas.MaxHp(Current) + ArmorBonus.Hp;
        public int MaxMp => DerivedStatFormulas.MaxMp(Current) + ArmorBonus.Mp;
        public float PhysicalAttack => DerivedStatFormulas.PhysicalAttack(Current) + ArmorBonus.PhysicalAttack;
        public float PhysicalDefense => DerivedStatFormulas.PhysicalDefense(Current) + ArmorBonus.PhysicalDefense;
        public float MagicAttack => DerivedStatFormulas.MagicAttack(Current) + ArmorBonus.MagicAttack;
        public float MagicDefense => DerivedStatFormulas.MagicDefense(Current) + ArmorBonus.MagicDefense;
        public int Initiative => DerivedStatFormulas.Initiative(Current) + ArmorBonus.Initiative;
        public int MoveRange => DerivedStatFormulas.MoveRange(Current) + ArmorBonus.MoveRange;
        public float StatusResist => Math.Min(DerivedStatFormulas.StatusResistCapWithGear,
            DerivedStatFormulas.StatusResist(Current) + ArmorBonus.StatusResist);
        public float CritChance => Math.Min(DerivedStatFormulas.CritCapWithGear,
            DerivedStatFormulas.CritChance(Current) + ArmorBonus.CritChance);

        public UnitStats(ClassDefinition classDefinition, int startingLevel = 1)
        {
            Class = classDefinition ?? throw new ArgumentNullException(nameof(classDefinition));
            Level = startingLevel;
            _body = classDefinition.BaseStats.Body;
            _mind = classDefinition.BaseStats.Mind;
            _spirit = classDefinition.BaseStats.Spirit;
        }

        public void LevelUp()
        {
            Level++;
            _body += Class.Growth.Body;
            _mind += Class.Growth.Mind;
            _spirit += Class.Growth.Spirit;
        }

        public bool TryEquipArmor(IArmor armor) => TryEquipArmor(armor, out _);

        public bool TryEquipArmor(IArmor armor, out IArmor previouslyEquipped)
        {
            previouslyEquipped = null;
            if (armor == null || !armor.CanEquip(Current)) return false;
            previouslyEquipped = EquippedArmor;
            EquippedArmor = armor;
            return true;
        }

        private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
