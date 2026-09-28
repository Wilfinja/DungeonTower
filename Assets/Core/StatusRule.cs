using System.Collections.Generic;

namespace DungeonTower.Core
{
    public enum StatusPolarity
    {
        // Resisted by the target's StatusResist (unless self-applied),
        // removed by Cleanse, and aimed at enemies.
        Harmful,
        // Never resisted, untouched by Cleanse, aimed at allies.
        Beneficial
    }

    public enum ReapplyMode
    {
        // Stacks are added (up to MaxStacks); duration refreshes to the
        // longer of old/new. Poison, Bleed, Regeneration.
        AddStacks,
        // Duration refreshes to the longer of old/new; nothing else
        // changes. Most timed statuses.
        RefreshDuration,
        // Magnitude becomes the higher of old/new; duration refreshes
        // to the longer of old/new. Burn.
        KeepStronger
    }

    /// <summary>
    /// The lifecycle rules for one status id — how it stacks on
    /// reapplication, and what ticks down when. Pure data, no
    /// behavior; anything that does something on a tick (damage, heal,
    /// detonate) lives in an IStatusBehavior instead.
    ///
    /// Three independent "countdowns" can exist on a status instance,
    /// and a status expires as soon as ANY countdown it uses hits zero:
    ///   - Stacks      (StackDecayPerTurn)      — ticks at the owner's turn START
    ///   - Magnitude   (MagnitudeDecayPerTurn)  — ticks at the owner's turn START
    ///   - Duration    (UsesDuration)           — ticks at turn END by default,
    ///                                            or turn START if
    ///                                            TicksDurationAtTurnStart
    /// A status that uses none of them (SecondWind) never expires on its
    /// own — something has to consume or remove it.
    /// </summary>
    public readonly struct StatusRule
    {
        public StatusPolarity Polarity { get; }
        public ReapplyMode Reapply { get; }
        public int MaxStacks { get; }
        public int StackDecayPerTurn { get; }
        public float MagnitudeDecayPerTurn { get; }
        public bool UsesDuration { get; }
        public bool TicksDurationAtTurnStart { get; }
        public int DefaultDuration { get; }

        public StatusRule(
            StatusPolarity polarity,
            ReapplyMode reapply,
            int maxStacks = 0,
            int stackDecayPerTurn = 0,
            float magnitudeDecayPerTurn = 0f,
            bool usesDuration = false,
            bool ticksDurationAtTurnStart = false,
            int defaultDuration = 0)
        {
            Polarity = polarity;
            Reapply = reapply;
            MaxStacks = maxStacks;
            StackDecayPerTurn = stackDecayPerTurn;
            MagnitudeDecayPerTurn = magnitudeDecayPerTurn;
            UsesDuration = usesDuration;
            TicksDurationAtTurnStart = ticksDurationAtTurnStart;
            DefaultDuration = defaultDuration;
        }
    }

    /// <summary>
    /// The rule table. Tuning lives here — one line per status — so
    /// balance changes never touch the tracker. Numbers marked (spec)
    /// come straight from the design; the rest are placeholders.
    /// </summary>
    public static class StatusRules
    {
        private static readonly Dictionary<StatusEffectId, StatusRule> Table = Build();

        public static StatusRule Get(StatusEffectId id) => Table[id];

        private static StatusRule Timed(StatusPolarity polarity, int defaultDuration,
            ReapplyMode reapply = ReapplyMode.RefreshDuration, bool atTurnStart = false)
            => new StatusRule(polarity, reapply, usesDuration: true,
                ticksDurationAtTurnStart: atTurnStart, defaultDuration: defaultDuration);

        private static Dictionary<StatusEffectId, StatusRule> Build()
        {
            var t = new Dictionary<StatusEffectId, StatusRule>();

            // --- Over-time effects ---
            // Poison (spec): stacks == damage per turn; loses 1 stack per turn.
            t[StatusEffectId.Poison] = new StatusRule(StatusPolarity.Harmful, ReapplyMode.AddStacks, stackDecayPerTurn: 1);
            // Regeneration (spec): reverse Poison.
            t[StatusEffectId.Regeneration] = new StatusRule(StatusPolarity.Beneficial, ReapplyMode.AddStacks, stackDecayPerTurn: 1);
            // Bleed (spec): stacks == damage per MOVE; loses 2 stacks per turn.
            t[StatusEffectId.Bleed] = new StatusRule(StatusPolarity.Harmful, ReapplyMode.AddStacks, stackDecayPerTurn: 2);
            // Burn (spec): Magnitude == % of max HP per turn; loses 5 points per turn.
            // Reapply = KeepStronger is an assumption ("similar to Poison" could
            // also mean the percentages add) — change if so.
            t[StatusEffectId.Burn] = new StatusRule(StatusPolarity.Harmful, ReapplyMode.KeepStronger, magnitudeDecayPerTurn: 5f);
            // Doom (spec): a countdown that detonates at 0. Ticks at turn START.
            t[StatusEffectId.Doom] = Timed(StatusPolarity.Harmful, 3, ReapplyMode.AddStacks, atTurnStart: true);

            // --- Timed stat modifiers ---
            t[StatusEffectId.Weaken] = Timed(StatusPolarity.Harmful, 2);
            t[StatusEffectId.Sunder] = Timed(StatusPolarity.Harmful, 2);
            t[StatusEffectId.Slow] = Timed(StatusPolarity.Harmful, 2);
            t[StatusEffectId.Haste] = Timed(StatusPolarity.Beneficial, 2);
            t[StatusEffectId.Fortify] = Timed(StatusPolarity.Beneficial, 2);

            // --- Control ---
            t[StatusEffectId.Root] = Timed(StatusPolarity.Harmful, 1);
            t[StatusEffectId.Stun] = Timed(StatusPolarity.Harmful, 1);
            t[StatusEffectId.Silence] = Timed(StatusPolarity.Harmful, 2);
            t[StatusEffectId.Taunt] = Timed(StatusPolarity.Harmful, 2);

            // --- Reactive / flags ---
            t[StatusEffectId.Mark] = Timed(StatusPolarity.Harmful, 3);
            t[StatusEffectId.Thorns] = Timed(StatusPolarity.Beneficial, 3);
            t[StatusEffectId.Momentum] = Timed(StatusPolarity.Beneficial, 1);
            t[StatusEffectId.Ward] = Timed(StatusPolarity.Beneficial, 3, ReapplyMode.KeepStronger);
            // Second Wind (spec): a one-shot save — no decay, lasts until consumed.
            t[StatusEffectId.SecondWind] = new StatusRule(StatusPolarity.Beneficial, ReapplyMode.RefreshDuration);

            return t;
        }
    }
}
