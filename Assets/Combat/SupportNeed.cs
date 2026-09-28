using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Whether an ally is a real candidate for a given support ability
    /// right now — the gate that decides "does this enemy actually have
    /// something useful to do," separate from EnemyTargeting's "which
    /// ally, once we know at least one qualifies." Assumes the ability
    /// already targets allies (AbilityTargeting.TargetsAllies) and the
    /// ally is alive; callers filter those before asking.
    /// </summary>
    public static class SupportNeed
    {
        public static bool Needs(CombatUnit ally, IAbility ability, float healThresholdPercent)
        {
            switch (ability.EffectKind)
            {
                case EffectKind.Heal:
                    return NeedsHeal(ally, ability, healThresholdPercent);

                case EffectKind.Cleanse:
                    return HasCleansable(ally, ability.Cleanses);

                case EffectKind.Status:
                    // TargetsAllies already guarantees every entry here is
                    // Beneficial — "needs it" means doesn't already have it,
                    // so a support unit doesn't waste a cast re-topping a
                    // buff that's still fully up.
                    return ability.Statuses.Any(app => !ally.Status.Has(app.Id));

                case EffectKind.Buff:
                    // A permanent-for-the-battle addition with no per-cast
                    // marker to check against — always "useful"; cooldown
                    // is what actually throttles how often it's cast.
                    return true;

                default:
                    return false;
            }
        }

        private static bool NeedsHeal(CombatUnit ally, IAbility ability, float thresholdPercent)
        {
            if (ability.HealHp > 0)
            {
                if (ally.CurrentHp >= ally.Stats.MaxHp) return false;
                float hpPercent = 100f * ally.CurrentHp / System.Math.Max(1, ally.Stats.MaxHp);
                return hpPercent <= thresholdPercent;
            }

            if (ability.HealMp > 0)
                return ally.CurrentMp < ally.Stats.MaxMp;

            return false;
        }

        private static bool HasCleansable(CombatUnit ally, System.Collections.Generic.IReadOnlyList<StatusEffectId> cleanses)
        {
            if (cleanses == null || cleanses.Count == 0)
                return ally.Status.Active.Any(i => StatusRules.Get(i.Id).Polarity == StatusPolarity.Harmful);

            return ally.Status.Active.Any(i => cleanses.Contains(i.Id));
        }
    }
}
