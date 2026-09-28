using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// The extension seam for statuses that DO something on a tick,
    /// beyond modifying stats: Poison/Burn/Regeneration damage or heal at
    /// turn start, Bleed hurts when the owner moves, Doom detonates on
    /// expiry, and so on. A behavior gets the owner and the live
    /// instance, may change the owner's HP directly, and reports what
    /// happened by appending to result.Log. It should NOT remove or
    /// decay its own instance — the tracker owns the lifecycle
    /// (decay/expiry follow the status's StatusRule), so behaviors stay
    /// purely "what happens," never "how long."
    ///
    /// Derive from StatusBehaviorBase and override only what you need.
    /// Statuses with no registered behavior (Weaken, Slow, Stun, ...)
    /// have no tick behavior; their effect is read directly off the
    /// tracker (stat bonus, CanAct, CanMove, Has()) or handled by
    /// DamagePipeline (Ward, Thorns, Mark, Momentum's damage bonus).
    /// </summary>
    public interface IStatusBehavior
    {
        // At the owner's turn start, BEFORE this status's stacks/magnitude
        // decay — so Poison at 3 stacks deals 3, then drops to 2.
        void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result);

        // Right after the owner moves (any move: player, chase, or roam).
        void OnMoved(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result);

        // When the status runs out (any countdown reached zero), before removal.
        void OnExpired(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result);
    }

    public abstract class StatusBehaviorBase : IStatusBehavior
    {
        public virtual void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result) { }
        public virtual void OnMoved(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result) { }
        public virtual void OnExpired(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result) { }
    }

    public static class StatusBehaviors
    {
        private static readonly Dictionary<StatusEffectId, IStatusBehavior> Registry
            = new Dictionary<StatusEffectId, IStatusBehavior>();

        static StatusBehaviors()
        {
            // Phase 2
            Register(StatusEffectId.Regeneration, new RegenerationBehavior());
            Register(StatusEffectId.Momentum, new MomentumBehavior());
            // Phase 3
            Register(StatusEffectId.Poison, new PoisonBehavior());
            Register(StatusEffectId.Burn, new BurnBehavior());
            Register(StatusEffectId.Bleed, new BleedBehavior());
            Register(StatusEffectId.Doom, new DoomBehavior());
        }

        public static IStatusBehavior Get(StatusEffectId id)
            => Registry.TryGetValue(id, out var behavior) ? behavior : null;

        public static void Register(StatusEffectId id, IStatusBehavior behavior)
            => Registry[id] = behavior;
    }
}
