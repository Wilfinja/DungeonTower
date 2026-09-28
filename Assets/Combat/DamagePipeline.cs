using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    public sealed class DamageOutcome
    {
        // After Momentum and Mark, before Ward — the number the hit "wanted" to deal.
        public int FinalDamage { get; internal set; }
        public int Absorbed { get; internal set; }
        // What actually came off the defender's HP.
        public int HpLost { get; internal set; }
        public bool SavedBySecondWind { get; internal set; }
        public int Reflected { get; internal set; }
        public List<string> Log { get; } = new List<string>();
    }

    /// <summary>
    /// Everything that happens between "AttackResolver rolled N damage"
    /// and "HP goes down": statuses that modify a hit, absorb it, save
    /// the defender from it, or punish the attacker for it. One place,
    /// one fixed order, so statuses interact predictably:
    ///
    ///   1. Momentum  (attacker) — armed by a move; +Magnitude% damage, spent on use
    ///   2. Mark      (defender) — +Magnitude% damage taken
    ///   3. Ward      (defender) — absorbs from its Magnitude pool; breaks at 0
    ///   4. HP loss   — Second Wind (defender) turns a killing blow into 1 HP, once
    ///   5. Thorns    (defender) — reflects Magnitude% of the damage that got through
    ///                             onto an ADJACENT attacker, bypassing steps 1-4
    ///
    /// Only ability hits come through here. Damage-over-time ticks
    /// (Poison, Burn, Bleed, Doom) call CombatUnit.ApplyDamage directly,
    /// so Ward, Mark and Thorns don't touch them — Second Wind still
    /// does, since it lives in ApplyDamage itself.
    /// </summary>
    public static class DamagePipeline
    {
        // Thorns only punishes attackers close enough to be "stabbed".
        public const int ThornsMaxRange = 1;

        public static DamageOutcome Apply(CombatUnit attacker, CombatUnit defender, int damage)
        {
            var outcome = new DamageOutcome();
            damage = Math.Max(0, damage);

            // 1. Momentum
            var momentum = attacker.Status.Get(StatusEffectId.Momentum);
            if (momentum != null && momentum.Primed && damage > 0)
            {
                momentum.Primed = false;
                int boosted = Scale(damage, momentum.Magnitude);
                outcome.Log.Add($"{attacker.DisplayName}'s momentum adds {boosted - damage} damage.");
                damage = boosted;
            }

            // 2. Mark
            var mark = defender.Status.Get(StatusEffectId.Mark);
            if (mark != null && damage > 0)
            {
                int boosted = Scale(damage, mark.Magnitude);
                if (boosted > damage)
                    outcome.Log.Add($"{defender.DisplayName} is marked, taking {boosted - damage} extra damage.");
                damage = boosted;
            }

            outcome.FinalDamage = damage;

            // 3. Ward
            var ward = defender.Status.Get(StatusEffectId.Ward);
            if (ward != null && damage > 0 && ward.Magnitude > 0f)
            {
                int absorbed = Math.Min(damage, (int)Math.Floor(ward.Magnitude));
                ward.Magnitude -= absorbed;
                damage -= absorbed;
                outcome.Absorbed = absorbed;
                if (absorbed > 0)
                    outcome.Log.Add($"{defender.DisplayName}'s ward absorbs {absorbed}.");
                if (ward.Magnitude < 1f)
                {
                    defender.Status.Remove(StatusEffectId.Ward);
                    outcome.Log.Add($"{defender.DisplayName}'s ward shatters.");
                }
            }

            // 4. HP loss (Second Wind lives inside ApplyDamage)
            int hpBefore = defender.CurrentHp;
            defender.ApplyDamage(damage, out bool saved);
            outcome.HpLost = hpBefore - defender.CurrentHp;
            outcome.SavedBySecondWind = saved;
            if (saved)
                outcome.Log.Add($"{defender.DisplayName} refuses to fall — Second Wind leaves them at 1 HP!");

            // 5. Thorns
            var thorns = defender.Status.Get(StatusEffectId.Thorns);
            if (thorns != null && defender.IsAlive && attacker != defender && attacker.IsAlive
                && outcome.HpLost > 0 && thorns.Magnitude > 0f
                && attacker.Position.ManhattanDistance(defender.Position) <= ThornsMaxRange)
            {
                int reflected = Math.Max(1, Scale(outcome.HpLost, thorns.Magnitude, bonusOnly: true));
                attacker.ApplyDamage(reflected, out bool attackerSaved);
                outcome.Reflected = reflected;
                outcome.Log.Add($"{defender.DisplayName}'s thorns reflect {reflected} damage onto {attacker.DisplayName}.");
                if (attackerSaved)
                    outcome.Log.Add($"{attacker.DisplayName} refuses to fall — Second Wind leaves them at 1 HP!");
            }

            return outcome;
        }

        // damage * (1 + percent/100), rounded — or just damage * percent/100
        // when bonusOnly (used for reflect, where the percent IS the share).
        private static int Scale(int amount, float percent, bool bonusOnly = false)
        {
            double factor = bonusOnly ? percent / 100.0 : 1.0 + percent / 100.0;
            return (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
        }
    }
}
