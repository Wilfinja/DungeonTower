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
    /// Resolves a single Damage-kind ability hitting one target. The
    /// ability's stat weights decide how hard it hits (weight x stat power,
    /// summed - see AbilityScaling), and its Kind decides which defense,
    /// Physical or Magic, is subtracted from that. No accuracy/evasion stat
    /// exists yet, so every attack is assumed to hit. Crit multiplier is a
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
            float attack = AbilityScaling.Total(attacker.Stats, ability);
            float defense = ability.Kind == AttackKind.Physical
                ? defender.Stats.PhysicalDefense
                : defender.Stats.MagicDefense;

            // Crit = the attacker's gear bonus + whatever this ability adds.
            float critChance = Math.Min(100f, attacker.Stats.CritChance + ability.CritChance);
            bool isCrit = rng.NextDouble() * 100.0 < critChance;

            float rawDamage = Math.Max(MinimumDamage, attack - defense);
            if (isCrit) rawDamage *= CritDamageMultiplier;

            return new AttackResult(isCrit, (int)Math.Round(rawDamage, MidpointRounding.AwayFromZero));
        }
    }
}
