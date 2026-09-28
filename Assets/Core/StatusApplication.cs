using System;

namespace DungeonTower.Core
{
    /// <summary>
    /// One "apply this status" entry authored on an ability. On an
    /// EffectKind.Status ability it's the whole payload; on any other
    /// EffectKind it's an on-hit proc — applied to each target the
    /// ability lands on (a Damage ability only procs if the target
    /// survives the hit).
    ///
    /// Plain public fields (same reason as DerivedStatBonus — Unity's
    /// Inspector only edits mutable public fields). Because a freshly
    /// added list element is all zeros, zero means "default" for the
    /// numeric fields rather than "literally zero":
    ///   Chance   0 (or less) = always applies; otherwise a percent, 1-100.
    ///   Stacks   0 (or less) = 1.
    ///   Duration 0 (or less) = the status's DefaultDuration from StatusRules.
    ///   Magnitude and StatBonus are used as-is (their meaning depends on
    ///   the status — Burn: % of max HP per turn; Ward: absorb pool;
    ///   stat-modifier statuses: StatBonus is the per-stack modifier).
    /// </summary>
    [Serializable]
    public struct StatusApplication
    {
        public StatusEffectId Id;
        public float Chance;
        public int Stacks;
        public int Duration;
        public float Magnitude;
        public DerivedStatBonus StatBonus;

        public float EffectiveChance => Chance <= 0f ? 100f : Math.Min(100f, Chance);
        public int EffectiveStacks => Math.Max(1, Stacks);

        public int EffectiveDuration(StatusRule rule)
            => Duration > 0 ? Duration : Math.Max(1, rule.DefaultDuration);
    }
}
