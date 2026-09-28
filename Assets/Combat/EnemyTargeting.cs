using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Turns an EnemyTargetingStrategy/SupportTargetingStrategy into an
    /// actual pick from a candidate list. Both entry points assume the
    /// caller already filtered candidates to "alive, in range, and the
    /// right side" (hostile vs. ally) — this only decides WHICH of
    /// those to prefer.
    /// </summary>
    public static class EnemyTargeting
    {
        public static CombatUnit PickHostileTarget(
            CombatUnit self, IReadOnlyList<CombatUnit> candidates, EnemyTargetingStrategy strategy)
        {
            switch (strategy)
            {
                case EnemyTargetingStrategy.LowestHp:
                    return Best(self, candidates, u => u.CurrentHp, minimize: true);
                case EnemyTargetingStrategy.LowestHpPercent:
                    return Best(self, candidates, HpPercent, minimize: true);
                case EnemyTargetingStrategy.LowestPhysicalDefense:
                    return Best(self, candidates, u => u.Stats.PhysicalDefense, minimize: true);
                case EnemyTargetingStrategy.LowestMagicDefense:
                    return Best(self, candidates, u => u.Stats.MagicDefense, minimize: true);
                case EnemyTargetingStrategy.HighestPhysicalAttack:
                    return Best(self, candidates, u => u.Stats.PhysicalAttack, minimize: false);
                case EnemyTargetingStrategy.HighestMagicAttack:
                    return Best(self, candidates, u => u.Stats.MagicAttack, minimize: false);
                default: // Nearest
                    return Nearest(self, candidates);
            }
        }

        public static CombatUnit PickSupportTarget(
            CombatUnit self, IReadOnlyList<CombatUnit> candidates, SupportTargetingStrategy strategy)
        {
            switch (strategy)
            {
                case SupportTargetingStrategy.LowestHp:
                    return Best(self, candidates, u => u.CurrentHp, minimize: true);
                case SupportTargetingStrategy.MostHarmfulStatuses:
                    return Best(self, candidates, CountHarmfulStatuses, minimize: false);
                case SupportTargetingStrategy.Nearest:
                    return Nearest(self, candidates);
                case SupportTargetingStrategy.Self:
                    return candidates.FirstOrDefault(c => c == self) ?? Nearest(self, candidates);
                default: // LowestHpPercent
                    return Best(self, candidates, HpPercent, minimize: true);
            }
        }

        private static float HpPercent(CombatUnit u) => (float)u.CurrentHp / Math.Max(1, u.Stats.MaxHp);

        private static float CountHarmfulStatuses(CombatUnit u)
            => u.Status.Active.Count(i => StatusRules.Get(i.Id).Polarity == StatusPolarity.Harmful);

        // Picks the candidate with the best (lowest, if minimize, else
        // highest) metric value; ties broken by distance to self, then
        // by list order, so the result is deterministic.
        private static CombatUnit Best(
            CombatUnit self, IReadOnlyList<CombatUnit> candidates, Func<CombatUnit, float> metric, bool minimize)
        {
            CombatUnit best = null;
            float bestValue = 0f;
            int bestDistance = int.MaxValue;

            foreach (var candidate in candidates)
            {
                float value = metric(candidate);
                int distance = self.Position.ManhattanDistance(candidate.Position);

                bool better = best == null
                    || (minimize ? value < bestValue : value > bestValue)
                    || (value == bestValue && distance < bestDistance);

                if (better) { best = candidate; bestValue = value; bestDistance = distance; }
            }

            return best;
        }

        private static CombatUnit Nearest(CombatUnit self, IReadOnlyList<CombatUnit> candidates)
            => candidates
                .OrderBy(c => self.Position.ManhattanDistance(c.Position))
                .FirstOrDefault();
    }
}
