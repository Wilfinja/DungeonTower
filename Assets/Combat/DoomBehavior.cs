using System;

namespace DungeonTower.Combat
{
    /// <summary>
    /// A ticking bomb: when the countdown reaches zero the owner takes
    /// Magnitude damage — only the doomed unit, no splash. The
    /// countdown itself (Duration, ticking at the owner's turn start) and
    /// the "further Doom hits add damage and shorten it by 2" rule live
    /// in StatusRules/StatusEffectTracker; this only says what happens
    /// on the countdown and on detonation. A Cleanse defuses it (the
    /// status is removed without OnExpired ever running).
    /// </summary>
    public sealed class DoomBehavior : StatusBehaviorBase
    {
        public override void OnTurnStart(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            // Duration hasn't been decremented yet this tick.
            int remaining = instance.Duration - 1;
            if (remaining > 0)
                result.Log.Add($"Doom looms over {owner.DisplayName} — {remaining} turn(s) until it claims {instance.Magnitude:0} HP.");
        }

        public override void OnExpired(CombatUnit owner, StatusEffectInstance instance, StatusTickResult result)
        {
            int damage = Math.Max(1, (int)Math.Round(instance.Magnitude, MidpointRounding.AwayFromZero));
            DotDamage.Deal(owner, damage,
                $"Doom claims {owner.DisplayName} for {damage} damage!", result);
        }
    }
}
