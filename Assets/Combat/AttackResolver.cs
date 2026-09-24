using System;
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
    /// Resolves a single Damage-kind ability hitting one target — the
    /// ability's Kind decides whether Physical or Magic Attack/Defense
    /// apply, and its DamageMultiplier scales the attacker's raw stat
    /// before defense is subtracted. No accuracy/evasion stat exists
    /// yet, so every attack is assumed to hit. Crit multiplier is a
    /// placeholder.
    ///
    /// Heal/Buff-kind abilities don't go through here at all — they're
    /// simple enough (no RNG, no defense) that BattleController applies
    /// them directly per target. AoE fan-out (which units in the
    /// footprint get hit) also lives in BattleController now, since that
    /// decision differs by EffectKind — see ExecuteAbilityAt.
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
    }
}
