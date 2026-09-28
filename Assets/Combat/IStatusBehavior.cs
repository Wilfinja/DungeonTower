using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// The extension seam for statuses that DO something on a tick,
    /// beyond modifying stats: Poison/Burn/Regeneration damage or heal at
    /// turn start, Doom detonates on expiry, and so on. A behavior gets
    /// the owner and the live instance, may change the owner's HP
    /// directly, and reports what happened by appending to result.Log.
    /// It should NOT remove or decay its own instance — the tracker owns
    /// the lifecycle (decay/expiry follow the status's StatusRule), so
    /// behaviors stay purely "what happens," never "how long."
    ///
    /// Statuses with no entry in StatusBehaviors (Weaken, Slow, Stun,
    /// ...) simply have no tick behavior; their effect is read directly
    /// off the tracker (stat bonus, CanAct, CanMove, Has()).
    /// </summary>
    public interface IStatusBehavior
    {
        // Called at the owner's turn start, BEFORE this status's stacks/
        // magnitude decay — so Poison at 3 stacks deals 3, then drops to 2.
        void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result);

        // Called when the status runs out (any countdown reached zero),
        // before it is removed.
        void OnExpired(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result);
    }

    public static class StatusBehaviors
    {
        private static readonly Dictionary<StatusEffectId, IStatusBehavior> Registry
            = new Dictionary<StatusEffectId, IStatusBehavior>();

        public static IStatusBehavior Get(StatusEffectId id)
            => Registry.TryGetValue(id, out var behavior) ? behavior : null;

        public static void Register(StatusEffectId id, IStatusBehavior behavior)
            => Registry[id] = behavior;

        // Phase 1 ships with no behaviors — the framework alone. Phases
        // 2 and 3 register theirs here.
    }
}
