using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Picks the best set of abilities for an enemy to spend its actions on.
    /// Pure logic (no scene access): BattleController describes each usable
    /// ability as an Option - what it costs in half-steps, MP, and how good
    /// it is (Score) - and this returns the combination with the highest
    /// total score that fits the budget. Abilities with a cooldown can be
    /// used once per phase; the rest can repeat. Ties go to the plan that
    /// spends more of the budget.
    ///
    /// This is what lets an enemy pick "one 2-action smash plus a 1-action
    /// jab" over three jabs when the smash is worth more per action.
    /// </summary>
    public static class EnemyAbilityPlanner
    {
        private const int MaxPicks = 12;
        private const float Epsilon = 0.0001f;

        public readonly struct Option
        {
            public IAbility Ability { get; }
            public int CostHalf { get; }
            public float Score { get; }
            public int MpCost { get; }
            public bool OncePerPhase { get; }

            public Option(IAbility ability, int costHalf, float score, int mpCost, bool oncePerPhase)
            {
                Ability = ability;
                CostHalf = costHalf;
                Score = score;
                MpCost = mpCost;
                OncePerPhase = oncePerPhase;
            }
        }

        // Best-scoring sequence that fits, highest score first (so the big
        // hit lands before the target can die to something smaller).
        // Empty when nothing fits or everything scores zero or less.
        public static List<Option> BestSequence(IReadOnlyList<Option> options, int budgetHalf, int mpAvailable)
        {
            var best = new List<int>();
            float bestScore = 0f;
            int bestSpent = 0;
            var current = new List<int>();

            void Go(int start, int budget, int mp, float score, int spent)
            {
                if (score > bestScore + Epsilon || (Math.Abs(score - bestScore) <= Epsilon && spent > bestSpent && score > 0f))
                {
                    bestScore = score;
                    bestSpent = spent;
                    best = new List<int>(current);
                }
                if (current.Count >= MaxPicks) return;

                for (int i = start; i < options.Count; i++)
                {
                    var o = options[i];
                    if (o.CostHalf > budget || o.MpCost > mp || o.Score <= 0f) continue;
                    current.Add(i);
                    Go(o.OncePerPhase ? i + 1 : i, budget - o.CostHalf, mp - o.MpCost, score + o.Score, spent + o.CostHalf);
                    current.RemoveAt(current.Count - 1);
                }
            }

            Go(0, budgetHalf, mpAvailable, 0f, 0);

            return best.Select(i => options[i]).OrderByDescending(o => o.Score).ToList();
        }
    }
}
