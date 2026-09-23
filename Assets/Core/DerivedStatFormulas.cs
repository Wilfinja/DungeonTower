using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// All Body/Mind/Spirit -> combat number conversions live here so tuning
    /// is a one-file job. Every constant is a placeholder from the initial
    /// design pass and expected to move once we're playtesting.
    /// </summary>
    public static class DerivedStatFormulas
    {
        // --- Body ---
        public const float HpPerBody = 4f;
        public const float PhysicalAttackPerBody = 1.5f;
        public const float PhysicalDefensePerBody = 0.5f;

        // --- Mind ---
        public const float MpPerMind = 3f;
        public const float MagicAttackPerMind = 1.5f;
        public const float MagicDefensePerMind = 0.5f;
        public const float MagicDefenseFloor = 2f; // stops 0-Mind units from being free damage

        // --- Spirit ---
        // Spirit drives two different combat numbers on two different scales —
        // initiative can be 1:1, but move range in tiles cannot be, so it gets
        // its own base + scaling below. Worth eyeballing once this is in play.
        public const float InitiativePerSpirit = 1f;
        public const float MoveRangeBase = 3f;         // tiles at 0 Spirit
        public const float MoveRangePerSpirit = 0.2f;   // +1 tile per 5 Spirit, placeholder
        public const float StatusResistPerSpirit = 4f;  // percent
        public const float StatusResistCap = 80f;       // percent, from stats alone
        public const float CritPerSpirit = 2f;          // percent
        public const float CritCap = 40f;                // percent, from stats alone

        // Ceilings once armor bonuses are added on top — gear can push a
        // unit past the stats-only cap above, but not infinitely.
        public const float StatusResistCapWithGear = 90f;
        public const float CritCapWithGear = 50f;

        public static int MaxHp(StatBlock stats) => RoundToInt(stats.Body * HpPerBody);
        public static float PhysicalAttack(StatBlock stats) => stats.Body * PhysicalAttackPerBody;
        public static float PhysicalDefense(StatBlock stats) => stats.Body * PhysicalDefensePerBody;

        public static int MaxMp(StatBlock stats) => RoundToInt(stats.Mind * MpPerMind);
        public static float MagicAttack(StatBlock stats) => stats.Mind * MagicAttackPerMind;

        public static float MagicDefense(StatBlock stats)
            => Math.Max(MagicDefenseFloor, stats.Mind * MagicDefensePerMind);

        public static int Initiative(StatBlock stats) => RoundToInt(stats.Spirit * InitiativePerSpirit);

        public static int MoveRange(StatBlock stats)
            => RoundToInt(MoveRangeBase + stats.Spirit * MoveRangePerSpirit);

        public static float StatusResist(StatBlock stats)
            => Math.Min(StatusResistCap, stats.Spirit * StatusResistPerSpirit);

        public static float CritChance(StatBlock stats)
            => Math.Min(CritCap, stats.Spirit * CritPerSpirit);

        private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
