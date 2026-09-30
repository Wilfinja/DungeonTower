namespace DungeonTower.Core
{
    /// <summary>
    /// The numbers behind the classless stat-point system, in one place so
    /// tuning is a one-file job (same idea as DerivedStatFormulas).
    /// Every hero starts identical, spends creation points, then gains
    /// points on each level-up that they allocate into Body/Mind/Spirit.
    /// </summary>
    public static class LevelUpRules
    {
        // Every hero starts here, before any points are spent.
        public const int StartingStatValue = 3;

        // Points spent once, at hero creation.
        public const int StartingPoints = 3;

        // Points granted by each level-up. Unspent points bank
        // indefinitely (see UnitStats.UnspentPoints).
        public const int PointsPerLevel = 3;

        // XP needed to go from `level` to `level + 1`: 50, 75, 100, ...
        // Each hero tracks their own XP toward this (UnitStats.Experience).
        public const int XpBase = 25;
        public const int XpPerLevel = 25;

        public static int XpToNextLevel(int level) => XpBase + XpPerLevel * System.Math.Max(1, level);

        // When an enemy dies, the hero who landed the killing blow earns
        // its full XP reward times KillerXpMultiplier; every OTHER living
        // hero earns the reward times PartyShareXpMultiplier. A kill by
        // a status tick or trap has no killer, so everyone living gets
        // the party share.
        public const float KillerXpMultiplier = 1.0f;
        public const float PartyShareXpMultiplier = 0.5f;

        public static StatBlock StartingStats
            => new StatBlock(StartingStatValue, StartingStatValue, StartingStatValue);
    }
}
