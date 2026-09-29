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

        public static StatBlock StartingStats
            => new StatBlock(StartingStatValue, StartingStatValue, StartingStatValue);
    }
}
