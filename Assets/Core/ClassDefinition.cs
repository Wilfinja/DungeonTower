using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// Static data for one class: its stat lean, starting stats, and per-level
    /// growth. Instances live in ClassLibrary — this type itself holds no
    /// runtime state.
    /// </summary>
    public sealed class ClassDefinition
    {
        public ClassId Id { get; }
        public string DisplayName { get; }
        public PrimaryStat PrimaryStat { get; }
        public PrimaryStat? SecondaryStat { get; }
        public StatBlock BaseStats { get; }
        public StatGrowth Growth { get; }

        public bool IsMixed => SecondaryStat.HasValue;

        public ClassDefinition(
            ClassId id,
            string displayName,
            PrimaryStat primaryStat,
            PrimaryStat? secondaryStat,
            StatBlock baseStats,
            StatGrowth growth)
        {
            if (secondaryStat.HasValue && secondaryStat.Value == primaryStat)
                throw new ArgumentException("Secondary stat must differ from primary stat.");

            Id = id;
            DisplayName = displayName;
            PrimaryStat = primaryStat;
            SecondaryStat = secondaryStat;
            BaseStats = baseStats;
            Growth = growth;
        }
    }
}
