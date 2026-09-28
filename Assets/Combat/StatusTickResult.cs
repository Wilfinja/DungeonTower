using System.Collections.Generic;

namespace DungeonTower.Combat
{
    /// <summary>
    /// What happened when a unit's statuses ticked at the start or end of
    /// its turn — handed back so BattleController (which owns death
    /// handling, loot, views, and logging) can react, while the tracker
    /// itself stays free of UI concerns.
    /// </summary>
    public sealed class StatusTickResult
    {
        // Human-readable lines for the log ("Goblin takes 3 poison damage.").
        // Behaviors append to this.
        public List<string> Log { get; } = new List<string>();

        // Statuses that ran out during this tick, already removed.
        public List<StatusEffectInstance> Expired { get; } = new List<StatusEffectInstance>();

        // The owner died during this tick (DoT, Doom detonation, ...).
        // The caller must run its normal death handling.
        public bool OwnerDied { get; internal set; }

        // Only set by OnTurnStart: the owner can't act this turn (Stun).
        // The caller should end the turn without offering any actions.
        public bool SkipTurn { get; internal set; }
    }
}
