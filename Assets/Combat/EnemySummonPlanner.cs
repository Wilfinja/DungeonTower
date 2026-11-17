using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Decides WHERE an enemy should place a Summon ability (totem, trap,
    /// cloud, aura) and whether it's worth casting at all. Pure logic with
    /// no scene access, like EnemyAbilityPlanner: BattleController hands in
    /// the legal anchor tiles plus where the two sides stand, and gets back
    /// the best tile and its score.
    ///
    /// What counts as "worth it":
    ///   OnRoundTick (totem, aura, hazard cloud): how many units it would
    ///     actually affect each round, i.e. opposing units when it's
    ///     hostile, the enemy's own side (caster included) when it's
    ///     beneficial, within AuraRadius of its footprint.
    ///   OnEntry (trap): how many hostile units are close enough to the
    ///     footprint to plausibly walk into it (TrapLureRange).
    ///   None (plain walls / obscuring clouds): never cast by enemies yet.
    /// A score below MinWorthwhile means "don't bother".
    /// </summary>
    public static class EnemySummonPlanner
    {
        // How close a hero has to be to a trap's tile for the trap to count as well placed.
        public const int TrapLureRange = 3;

        // At least one unit has to benefit (or be hit) for a cast to be worth an action.
        public const float MinWorthwhile = 1f;

        public static float Score(
            IAbility ability, GridPosition caster, GridPosition anchor,
            IReadOnlyList<GridPosition> hostiles, IReadOnlyList<GridPosition> allies)
        {
            // A following aura always sits on the caster.
            var center = ability.SummonFollowsOwner ? caster : anchor;
            var footprint = AreaOfEffect.GetAffectedTiles(caster, center, ability.AreaShape, ability.AreaRadius);

            switch (ability.SummonTriggerMode)
            {
                case SummonTriggerMode.OnRoundTick:
                {
                    var pool = ability.SummonAffectsAllies ? allies : hostiles;
                    return pool.Count(p => footprint.Any(t => p.ManhattanDistance(t) <= ability.AuraRadius));
                }

                case SummonTriggerMode.OnEntry:
                {
                    int lured = hostiles.Count(h => footprint.Any(t => h.ManhattanDistance(t) <= TrapLureRange));
                    if (lured == 0) return 0f;

                    // Of equally lured spots, the one closest to a hero is a little better.
                    int nearest = hostiles.Min(h => footprint.Min(t => h.ManhattanDistance(t)));
                    return lured + 1f / (1 + nearest);
                }

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// The best of `candidates` for this ability, if any is worth casting.
        /// Equal scores go to the tile nearest the caster.
        /// </summary>
        public static bool TryChooseAnchor(
            IAbility ability, GridPosition caster, IEnumerable<GridPosition> candidates,
            IReadOnlyList<GridPosition> hostiles, IReadOnlyList<GridPosition> allies,
            out GridPosition anchor, out float score)
        {
            anchor = caster;
            score = 0f;
            int bestDistance = int.MaxValue;
            bool found = false;

            foreach (var tile in candidates)
            {
                float s = Score(ability, caster, tile, hostiles, allies);
                if (s < MinWorthwhile) continue;

                int distance = caster.ManhattanDistance(tile);
                if (!found || s > score + 0.0001f || (System.Math.Abs(s - score) <= 0.0001f && distance < bestDistance))
                {
                    found = true;
                    anchor = tile;
                    score = s;
                    bestDistance = distance;
                }
            }
            return found;
        }
    }
}
