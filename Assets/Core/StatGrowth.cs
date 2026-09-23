namespace DungeonTower.Core
{
    /// <summary>
    /// Per-level growth amounts for each stat. Values are floats because a
    /// secondary stat's growth (50% of a class's base growth rate) usually
    /// isn't a whole number — the fractional part accumulates across levels
    /// in UnitStats and only rounds when a derived stat is read.
    /// </summary>
    public readonly struct StatGrowth
    {
        public float Body { get; }
        public float Mind { get; }
        public float Spirit { get; }

        public StatGrowth(float body, float mind, float spirit)
        {
            Body = body;
            Mind = mind;
            Spirit = spirit;
        }
    }
}
