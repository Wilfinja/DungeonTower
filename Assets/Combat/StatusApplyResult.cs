namespace DungeonTower.Combat
{
    public enum StatusApplyOutcome
    {
        Applied,     // new status on the target
        Stacked,     // already present; stacks were added
        Refreshed,   // already present; duration/magnitude refreshed
        ProcFailed,  // the ability's Chance roll missed
        Resisted     // the target's StatusResist roll succeeded
    }

    public readonly struct StatusApplyResult
    {
        public StatusApplyOutcome Outcome { get; }

        // The live instance on the target; null for ProcFailed/Resisted.
        public StatusEffectInstance Instance { get; }

        public bool Succeeded => Outcome == StatusApplyOutcome.Applied
            || Outcome == StatusApplyOutcome.Stacked
            || Outcome == StatusApplyOutcome.Refreshed;

        public StatusApplyResult(StatusApplyOutcome outcome, StatusEffectInstance instance)
        {
            Outcome = outcome;
            Instance = instance;
        }
    }
}
