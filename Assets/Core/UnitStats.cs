using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// Runtime stat state for one unit: its level, Body/Mind/Spirit,
    /// unspent stat points, equipped armor, and any Buff-kind ability
    /// bonuses picked up during the fight (potions, buff scrolls).
    /// There are no classes: every hero starts from the same stats and
    /// each level-up grants points (LevelUpRules.PointsPerLevel) that
    /// the player spends on Body, Mind, or Spirit. Unspent points bank.
    ///
    /// Primary stats (Body/Mind/Spirit, and Current below) only ever grow
    /// by spending points. Armor and buffs never touch them — they add a
    /// flat bonus directly onto the derived stats instead, applied on top
    /// of the formula result in each property below.
    /// </summary>
    public sealed class UnitStats
    {
        private int _body;
        private int _mind;
        private int _spirit;

        // Accumulated Buff-kind ability bonuses (potions, buff scrolls) —
        // permanent for the rest of this battle. There's no expiry/decay
        // yet, and nothing currently resets this between battles either,
        // since there's no "next battle" flow to reset it at — worth
        // revisiting once one exists.
        private DerivedStatBonus _consumableBonus;
        private DerivedStatBonus _temporaryBonus;

        public int Level { get; private set; }

        // Points earned but not yet spent. Banks indefinitely.
        public int UnspentPoints { get; private set; }

        public IArmor EquippedArmor { get; private set; }

        public int Body => _body;
        public int Mind => _mind;
        public int Spirit => _spirit;

        public StatBlock Current => new StatBlock(Body, Mind, Spirit);

        private DerivedStatBonus TotalBonus => (EquippedArmor?.PassiveBonus ?? default) + _consumableBonus + _temporaryBonus;

        public int MaxHp => Math.Max(1, DerivedStatFormulas.MaxHp(Current) + TotalBonus.Hp);
        public int MaxMp => Math.Max(0, DerivedStatFormulas.MaxMp(Current) + TotalBonus.Mp);
        public float PhysicalAttack => Math.Max(0f, DerivedStatFormulas.PhysicalAttack(Current) + TotalBonus.PhysicalAttack);
        public float PhysicalDefense => Math.Max(0f, DerivedStatFormulas.PhysicalDefense(Current) + TotalBonus.PhysicalDefense);
        public float MagicAttack => Math.Max(0f, DerivedStatFormulas.MagicAttack(Current) + TotalBonus.MagicAttack);
        public float MagicDefense => Math.Max(0f, DerivedStatFormulas.MagicDefense(Current) + TotalBonus.MagicDefense);
        public int Initiative => Math.Max(0, DerivedStatFormulas.Initiative(Current) + TotalBonus.Initiative);
        public int MoveRange => Math.Max(0, DerivedStatFormulas.MoveRange(Current) + TotalBonus.MoveRange);
        public float StatusResist => Math.Max(0f, Math.Min(DerivedStatFormulas.StatusResistCapWithGear,
            DerivedStatFormulas.StatusResist(Current) + TotalBonus.StatusResist));
        public float CritChance => Math.Max(0f, Math.Min(DerivedStatFormulas.CritCapWithGear,
            DerivedStatFormulas.CritChance(Current) + TotalBonus.CritChance));

        public UnitStats(StatBlock startingStats, int startingLevel = 1, int startingPoints = 0)
        {
            Level = startingLevel;
            _body = startingStats.Body;
            _mind = startingStats.Mind;
            _spirit = startingStats.Spirit;
            UnspentPoints = Math.Max(0, startingPoints);
        }

        // Leveling no longer changes stats directly — it grants points
        // the player spends with TrySpendPoint.
        public void LevelUp()
        {
            Level++;
            UnspentPoints += LevelUpRules.PointsPerLevel;
        }

        // Spends one banked point on the given stat. No respecs — a
        // spent point is permanent.
        public bool TrySpendPoint(PrimaryStat stat)
        {
            if (UnspentPoints <= 0) return false;

            switch (stat)
            {
                case PrimaryStat.Body: _body++; break;
                case PrimaryStat.Mind: _mind++; break;
                case PrimaryStat.Spirit: _spirit++; break;
                default: return false;
            }

            UnspentPoints--;
            return true;
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

        // Takes the armor off (leaving the slot empty) and hands it back.
        public IArmor UnequipArmor()
        {
            var previous = EquippedArmor;
            EquippedArmor = null;
            return previous;
        }

        // Applied by a Buff-kind ability (a potion or buff scroll) —
        // stacks additively with whatever's already accumulated.
        public void AddBonus(DerivedStatBonus bonus) => _consumableBonus += bonus;

        public void SetTemporaryBonus(DerivedStatBonus bonus) => _temporaryBonus = bonus;
    }
}
