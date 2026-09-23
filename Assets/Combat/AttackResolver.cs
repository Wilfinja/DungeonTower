using System;
using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    public readonly struct AttackResult
    {
        public bool IsCrit { get; }
        public int Damage { get; }

        public AttackResult(bool isCrit, int damage)
        {
            IsCrit = isCrit;
            Damage = damage;
        }
    }

    /// <summary>
    /// Resolves a single attack using a specific ability from the
    /// attacker's equipped weapon — the ability's Kind decides whether
    /// Physical or Magic Attack/Defense apply, and its DamageMultiplier
    /// scales the attacker's raw stat before defense is subtracted. No
    /// accuracy/evasion stat exists yet, so every attack is assumed to
    /// hit. Crit multiplier is a placeholder.
    /// </summary>
    public static class AttackResolver
    {
        public const float CritDamageMultiplier = 1.5f;
        public const int MinimumDamage = 1;

        public static AttackResult Resolve(CombatUnit attacker, CombatUnit defender, IAbility ability, Random rng)
        {
            float attack = ability.Kind == AttackKind.Physical
                ? attacker.Stats.PhysicalAttack
                : attacker.Stats.MagicAttack;
            float defense = ability.Kind == AttackKind.Physical
                ? defender.Stats.PhysicalDefense
                : defender.Stats.MagicDefense;

            bool isCrit = rng.NextDouble() * 100.0 < attacker.Stats.CritChance;

            float rawDamage = Math.Max(MinimumDamage, attack * ability.DamageMultiplier - defense);
            if (isCrit) rawDamage *= CritDamageMultiplier;

            return new AttackResult(isCrit, (int)Math.Round(rawDamage, MidpointRounding.AwayFromZero));
        }

        // AoE variant: resolves the same ability against every living
        // unit standing in its footprint (via AreaOfEffect), excluding
        // the attacker itself. Deliberately does NOT exclude the
        // attacker's own faction — a Blast/Line/Cone can catch allies
        // caught in the radius, same as most tactics games. Restrict the
        // candidate list to one faction at the call site if you want
        // friendly fire off for a particular ability.
        public static List<(CombatUnit Target, AttackResult Result)> ResolveAoE(
            CombatUnit attacker, IAbility ability, GridPosition impactTile,
            IEnumerable<CombatUnit> candidates, Random rng)
        {
            var affectedTiles = new HashSet<GridPosition>(
                AreaOfEffect.GetAffectedTiles(attacker.Position, impactTile, ability.AreaShape, ability.AreaRadius));

            return candidates
                .Where(u => u.IsAlive && u != attacker && affectedTiles.Contains(u.Position))
                .Select(u => (u, Resolve(attacker, u, ability, rng)))
                .ToList();
        }
    }
}
