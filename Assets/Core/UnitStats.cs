using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// Runtime stat state for one unit: its class, level, accumulated
    /// Body/Mind/Spirit, equipped armor, and any Buff-kind ability
    /// bonuses picked up during the fight (potions, buff scrolls).
    /// Growth accumulates as float internally so a small secondary-stat
    /// growth per level doesn't round away to nothing — only the
    /// exposed stat values round.
    ///
    /// Primary stats (Body/Mind/Spirit, and Current below) only ever grow
    /// from leveling. Armor and buffs never touch them — they add a flat
    /// bonus directly onto the derived stats instead, applied on top of
    /// the formula result in each property below.
    /// </summary>
    public sealed class UnitStats
    {
        private float _body;
        private float _mind;
        private float _spirit;

        // Accumulated Buff-kind ability bonuses (potions, buff scrolls) —
        // permanent for the rest of this battle. There's no expiry/decay
        // yet, and nothing currently resets this between battles either,
        // since there's no "next battle" flow to reset it at — worth
        // revisiting once one exists.
        private DerivedStatBonus _consumableBonus;

        public ClassDefinition Class { get; }
        public int Level { get; private set; }
        public IArmor EquippedArmor { get; private set; }

        public int Body => RoundToInt(_body);
        public int Mind => RoundToInt(_mind);
        public int Spirit => RoundToInt(_spirit);

        public StatBlock Current => new StatBlock(Body, Mind, Spirit);

        private DerivedStatBonus TotalBonus => (EquippedArmor?.PassiveBonus ?? default) + _consumableBonus;

        public int MaxHp => DerivedStatFormulas.MaxHp(Current) + TotalBonus.Hp;
        public int MaxMp => DerivedStatFormulas.MaxMp(Current) + TotalBonus.Mp;
        public float PhysicalAttack => DerivedStatFormulas.PhysicalAttack(Current) + TotalBonus.PhysicalAttack;
        public float PhysicalDefense => DerivedStatFormulas.PhysicalDefense(Current) + TotalBonus.PhysicalDefense;
        public float MagicAttack => DerivedStatFormulas.MagicAttack(Current) + TotalBonus.MagicAttack;
        public float MagicDefense => DerivedStatFormulas.MagicDefense(Current) + TotalBonus.MagicDefense;
        public int Initiative => DerivedStatFormulas.Initiative(Current) + TotalBonus.Initiative;
        public int MoveRange => DerivedStatFormulas.MoveRange(Current) + TotalBonus.MoveRange;
        public float StatusResist => Math.Min(DerivedStatFormulas.StatusResistCapWithGear,
            DerivedStatFormulas.StatusResist(Current) + TotalBonus.StatusResist);
        public float CritChance => Math.Min(DerivedStatFormulas.CritCapWithGear,
            DerivedStatFormulas.CritChance(Current) + TotalBonus.CritChance);

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

        // Applied by a Buff-kind ability (a potion or buff scroll) —
        // stacks additively with whatever's already accumulated.
        public void AddBonus(DerivedStatBonus bonus) => _consumableBonus += bonus;

        private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
