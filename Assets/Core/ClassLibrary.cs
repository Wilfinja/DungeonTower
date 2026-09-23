using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Builds and hands out the six locked ClassDefinitions. The shape
    /// (primary stat = full growth, secondary stat = 50%) is locked; the
    /// actual numbers below are first-pass placeholders for playtesting.
    /// </summary>
    public static class ClassLibrary
    {
        private const float PrimaryGrowth = 2.0f;
        private const float SecondaryGrowthMultiplier = 0.5f; // locked ratio
        private const float SecondaryGrowth = PrimaryGrowth * SecondaryGrowthMultiplier;
        private const float OffGrowth = 0.5f; // slow trickle on a class's untouched stat

        private static readonly Dictionary<ClassId, ClassDefinition> _classes = BuildAll();

        public static ClassDefinition Get(ClassId id) => _classes[id];

        public static IReadOnlyCollection<ClassDefinition> All => _classes.Values;

        private static Dictionary<ClassId, ClassDefinition> BuildAll()
        {
            return new Dictionary<ClassId, ClassDefinition>
            {
                // --- Pure classes ---
                [ClassId.Warrior] = new ClassDefinition(
                    ClassId.Warrior, "Warrior",
                    PrimaryStat.Body, null,
                    baseStats: new StatBlock(body: 8, mind: 3, spirit: 4),
                    growth: new StatGrowth(body: PrimaryGrowth, mind: OffGrowth, spirit: OffGrowth)),

                [ClassId.Adept] = new ClassDefinition(
                    ClassId.Adept, "Adept",
                    PrimaryStat.Mind, null,
                    baseStats: new StatBlock(body: 3, mind: 8, spirit: 4),
                    growth: new StatGrowth(body: OffGrowth, mind: PrimaryGrowth, spirit: OffGrowth)),

                [ClassId.Scout] = new ClassDefinition(
                    ClassId.Scout, "Scout",
                    PrimaryStat.Spirit, null,
                    baseStats: new StatBlock(body: 4, mind: 3, spirit: 8),
                    growth: new StatGrowth(body: OffGrowth, mind: OffGrowth, spirit: PrimaryGrowth)),

                // --- Mixed classes ---
                [ClassId.Spellblade] = new ClassDefinition(
                    ClassId.Spellblade, "Spellblade",
                    PrimaryStat.Body, PrimaryStat.Mind,
                    baseStats: new StatBlock(body: 7, mind: 5, spirit: 3),
                    growth: new StatGrowth(body: PrimaryGrowth, mind: SecondaryGrowth, spirit: OffGrowth)),

                [ClassId.Duelist] = new ClassDefinition(
                    ClassId.Duelist, "Duelist",
                    PrimaryStat.Body, PrimaryStat.Spirit,
                    baseStats: new StatBlock(body: 7, mind: 3, spirit: 5),
                    growth: new StatGrowth(body: PrimaryGrowth, mind: OffGrowth, spirit: SecondaryGrowth)),

                [ClassId.Mystic] = new ClassDefinition(
                    ClassId.Mystic, "Mystic",
                    PrimaryStat.Mind, PrimaryStat.Spirit,
                    baseStats: new StatBlock(body: 3, mind: 7, spirit: 5),
                    growth: new StatGrowth(body: OffGrowth, mind: PrimaryGrowth, spirit: SecondaryGrowth)),
            };
        }
    }
}
