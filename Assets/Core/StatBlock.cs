using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// A raw Body/Mind/Spirit reading for a unit at a point in time.
    /// Immutable — level-ups produce a new reading rather than mutating this one.
    /// </summary>
    public readonly struct StatBlock
    {
        public int Body { get; }
        public int Mind { get; }
        public int Spirit { get; }

        public StatBlock(int body, int mind, int spirit)
        {
            Body = body;
            Mind = mind;
            Spirit = spirit;
        }

        public int Get(PrimaryStat stat)
        {
            switch (stat)
            {
                case PrimaryStat.Body: return Body;
                case PrimaryStat.Mind: return Mind;
                case PrimaryStat.Spirit: return Spirit;
                default: throw new ArgumentOutOfRangeException(nameof(stat));
            }
        }

        public static StatBlock operator +(StatBlock a, StatBlock b)
            => new StatBlock(a.Body + b.Body, a.Mind + b.Mind, a.Spirit + b.Spirit);

        public override string ToString() => $"Body {Body} / Mind {Mind} / Spirit {Spirit}";
    }
}
