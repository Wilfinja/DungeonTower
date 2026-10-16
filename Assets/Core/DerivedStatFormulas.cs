using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// All Body/Mind/Spirit -> combat number conversions live here so tuning
    /// is a one-file job. Every constant is a placeholder from the initial
    /// design pass and expected to move once we're playtesting.
    ///
    /// Who owns what:
    ///   Body   - Max HP, Physical Attack/Defense, healing received, 40% of Initiative
    ///   Mind   - Max MP, Magic Attack/Defense, 35% of Initiative
    ///   Spirit - Status Resist, Potency, Support Power, MP regen, 25% of Initiative
    /// Move Range and Crit Chance no longer come from stats at all: Move is a
    /// fixed base (plus gear/statuses) and Crit comes from the ability used
    /// (plus gear).
    /// </summary>
    public static class DerivedStatFormulas
    {
        // --- Body ---
        public const float HpPerBody = 4f;
        public const float PhysicalAttackPerBody = 1.5f;
        public const float PhysicalDefensePerBody = 0.5f;
        public const float HealingReceivedPerBody = 3f;   // percent more HP from every heal

        // --- Mind ---
        public const float MpPerMind = 3f;
        public const float MagicAttackPerMind = 1.5f;
        public const float MagicDefensePerMind = 0.5f;
        public const float MagicDefenseFloor = 2f; // stops 0-Mind units from being free damage

        // --- Spirit ---
        public const float StatusResistPerSpirit = 4f;  // percent
        public const float StatusResistCap = 80f;       // percent, from stats alone
        public const float PotencyPerSpirit = 2.5f;     // percent: added to status apply chance AND scales status strength
        public const float SupportPowerPerSpirit = 1.5f; // same scale as attack; feeds Heal abilities
        public const float MpRegenPerSpirit = 1f / 3f;   // MP per own turn (in combat); fractions carry over

        // --- Initiative: every stat contributes, Body most and Spirit least ---
        public const float InitiativeWeightBody = 0.40f;
        public const float InitiativeWeightMind = 0.35f;
        public const float InitiativeWeightSpirit = 0.25f;
        // 1 keeps a 3/3/3 hero at Initiative 3, same as before. Raise it for finer
        // turn-order resolution (fewer ties) without changing who goes first.
        public const float InitiativeScale = 1f;

        // --- Movement: no stat contribution any more ---
        public const float MoveRangeBase = 4f;          // tiles, for everyone; gear and statuses modify it

        // Ceilings once armor bonuses are added on top — gear can push a
        // unit past the stats-only cap above, but not infinitely.
        public const float StatusResistCapWithGear = 90f;
        public const float CritCapWithGear = 50f;

        public static int MaxHp(StatBlock stats) => RoundToInt(stats.Body * HpPerBody);
        public static float PhysicalAttack(StatBlock stats) => stats.Body * PhysicalAttackPerBody;
        public static float PhysicalDefense(StatBlock stats) => stats.Body * PhysicalDefensePerBody;
        public static float HealingReceived(StatBlock stats) => stats.Body * HealingReceivedPerBody;

        public static int MaxMp(StatBlock stats) => RoundToInt(stats.Mind * MpPerMind);
        public static float MagicAttack(StatBlock stats) => stats.Mind * MagicAttackPerMind;

        public static float MagicDefense(StatBlock stats)
            => Math.Max(MagicDefenseFloor, stats.Mind * MagicDefensePerMind);

        public static float StatusResist(StatBlock stats)
            => Math.Min(StatusResistCap, stats.Spirit * StatusResistPerSpirit);

        public static float Potency(StatBlock stats) => stats.Spirit * PotencyPerSpirit;
        public static float SupportPower(StatBlock stats) => stats.Spirit * SupportPowerPerSpirit;
        public static float MpRegen(StatBlock stats) => stats.Spirit * MpRegenPerSpirit;

        public static int Initiative(StatBlock stats)
            => RoundToInt(InitiativeScale * (stats.Body * InitiativeWeightBody
                                             + stats.Mind * InitiativeWeightMind
                                             + stats.Spirit * InitiativeWeightSpirit));

        public static int MoveRange(StatBlock stats) => RoundToInt(MoveRangeBase);

        private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
