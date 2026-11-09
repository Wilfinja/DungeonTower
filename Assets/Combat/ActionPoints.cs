using System;

namespace DungeonTower.Combat
{
    /// <summary>
    /// A hero's actions for their side's phase. Everything is counted in
    /// HALF-STEPS, the same unit GridSteps uses for movement, so a straight
    /// step is 2 (one action), a diagonal step is 3 (one and a half), and an
    /// ability costs its ActionCost x 2. A hero gets BaseActions per phase
    /// (3), plus whatever they banked from their last phase, plus or minus
    /// Haste/Slow.
    ///
    /// Banking: whatever is left when the phase ends carries over, up to
    /// MaxBankedHalf (1 action). Half an action left over becomes 3.5
    /// actions next phase; three unspent actions still only bank one, so a
    /// hero can have at most 4 (more with Haste).
    ///
    /// Enemies use it too (no banking, since they never call EndPhase), with
    /// BaseActionsPerPhase set from their EnemySO.
    /// </summary>
    public sealed class ActionPoints
    {
        public const int HalfPerAction = GridSteps.StraightCost;
        public const int BaseActions = 3;
        public const int MaxBankedHalf = HalfPerAction;      // 1 action

        private int _carry;   // banked at the end of the last phase

        /// <summary>Actions this unit gets each phase before banking and Haste/Slow. Heroes use the default (3); enemies take theirs from EnemySO.</summary>
        public int BaseActionsPerPhase { get; set; } = BaseActions;

        /// <summary>Half-steps left this phase.</summary>
        public int Remaining { get; private set; }

        /// <summary>How much of this phase's pool was banked from the last one (for display).</summary>
        public int Banked { get; private set; }

        /// <summary>The hero ended their turn early (Done / stunned / rooted and holding).</summary>
        public bool IsDone { get; private set; }

        /// <summary>Enough left for the cheapest thing a hero can do: one straight step or a 1-action ability.</summary>
        public bool CanAct => !IsDone && Remaining >= HalfPerAction;

        public bool CanSpend(int halfSteps) => !IsDone && halfSteps <= Remaining;

        public bool CanSpendActions(int actions) => CanSpend(actions * HalfPerAction);

        /// <summary>Start of the hero's phase. bonusActions is +1 for Haste, -1 for Slow, etc.</summary>
        public void BeginPhase(int bonusActions = 0)
        {
            Banked = _carry;
            int pool = Math.Max(HalfPerAction, (BaseActionsPerPhase + bonusActions) * HalfPerAction);
            Remaining = pool + _carry;
            _carry = 0;
            IsDone = false;
        }

        /// <summary>End of the hero's phase: bank what's left (up to the cap) and empty the pool.</summary>
        public void EndPhase()
        {
            _carry = Math.Min(Math.Max(0, Remaining), MaxBankedHalf);
            Remaining = 0;
            Banked = 0;
            IsDone = true;
        }

        public void Spend(int halfSteps)
        {
            Remaining = Math.Max(0, Remaining - Math.Max(0, halfSteps));
        }

        public void SpendActions(int actions) => Spend(actions * HalfPerAction);

        /// <summary>The hero is finished for this phase; whatever is left will be banked at phase end.</summary>
        public void MarkDone() => IsDone = true;

        /// <summary>"2.5" style text for the UI.</summary>
        public static string Format(int halfSteps)
            => (halfSteps / (float)HalfPerAction).ToString(halfSteps % HalfPerAction == 0 ? "0" : "0.0");
    }
}
