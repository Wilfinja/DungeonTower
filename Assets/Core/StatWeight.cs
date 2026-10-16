using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// One term of an ability's scaling: "Weight x this stat's power".
    /// The power of each stat is the number that stat already feeds on the
    /// character sheet: Body -> Physical Attack, Mind -> Magic Attack,
    /// Spirit -> Support Power (all three grow 1.5 per point, so equal
    /// weights are equal value). Gear bonuses to those numbers count too.
    /// Plain public fields so Unity's Inspector can edit them in a list.
    /// </summary>
    [Serializable]
    public struct StatWeight
    {
        public PrimaryStat Stat;
        public float Weight;

        public StatWeight(PrimaryStat stat, float weight)
        {
            Stat = stat;
            Weight = weight;
        }
    }
}
