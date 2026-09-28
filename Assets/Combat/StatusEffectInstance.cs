using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One active status on one unit. Which of the numeric fields
    /// matter depends on the status's StatusRule (see StatusRule's
    /// doc comment) — unused ones just sit at their initial values.
    /// Mutated only by StatusEffectTracker (setters are internal).
    /// </summary>
    public sealed class StatusEffectInstance
    {
        public StatusEffectId Id { get; }

        // Poison/Bleed/Regeneration: the stack count IS the magnitude
        // of each tick. Stat-modifier statuses: multiplies StatBonus.
        public int Stacks { get; internal set; }

        // Turns left — only meaningful when the rule UsesDuration.
        public int Duration { get; internal set; }

        // Status-specific: Burn = % of max HP per turn, Ward = remaining
        // absorb pool, etc. A float so Burn's 5-point decay stays exact.
        public float Magnitude { get; internal set; }

        // Per-stack stat modifier — what Weaken/Sunder/Slow/Haste/Fortify
        // actually do. Zero for statuses that don't touch stats.
        public DerivedStatBonus StatBonus { get; internal set; }

        // Who applied it (most recent application). Needed by statuses
        // that point back at a specific unit — Taunt, Doom credit, etc.
        public CombatUnit Source { get; internal set; }

        // True when applied during the owner's own turn to a status whose
        // duration ticks at turn END — without this, a self-buff would
        // lose a turn of duration before the owner ever benefits from it.
        internal bool SkipNextDurationTick { get; set; }

        public DerivedStatBonus EffectiveBonus => StatBonus.Scaled(Stacks);

        internal StatusEffectInstance(StatusEffectId id, int stacks, int duration,
            float magnitude, DerivedStatBonus statBonus, CombatUnit source)
        {
            Id = id;
            Stacks = stacks;
            Duration = duration;
            Magnitude = magnitude;
            StatBonus = statBonus;
            Source = source;
        }
    }
}
