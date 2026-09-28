using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Taunt: a taunted unit must focus whoever taunted it. The taunter
    /// is the status's Source (whoever most recently applied it).
    /// Returns null — meaning "no restriction" — when the taunter is
    /// dead or isn't on the other side, so a stale taunt never traps a
    /// unit into chasing a corpse.
    /// </summary>
    public static class TauntRules
    {
        public static CombatUnit GetForcedTarget(CombatUnit unit)
        {
            var taunt = unit.Status.Get(StatusEffectId.Taunt);
            var source = taunt?.Source;
            if (source == null || !source.IsAlive || source.Faction == unit.Faction) return null;
            return source;
        }
    }
}
