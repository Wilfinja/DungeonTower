using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonTower.Core
{
    /// <summary>
    /// The one place that turns an ability's stat weights into a number.
    ///
    ///   power = sum over the ability's weights of (weight x that stat's power)
    ///
    /// Damage is power minus the target's defense (Physical or Magic, chosen
    /// by the ability's Kind). A heal is its flat HP plus power. Balance
    /// guide the Inspector shows: a pure one-stat ability totals about
    /// PureWeight, and a hybrid spreading over two or more stats totals about
    /// PureWeight x HybridWeightFactor, so balanced builds out-damage
    /// lopsided ones with hybrids while lopsided builds keep the best pure
    /// abilities.
    /// </summary>
    public static class AbilityScaling
    {
        public const float PureWeight = 1.0f;
        public const float HybridWeightFactor = 1.2f;

        public static float PowerOf(UnitStats stats, PrimaryStat stat)
        {
            if (stats == null) return 0f;
            switch (stat)
            {
                case PrimaryStat.Body: return stats.PhysicalAttack;
                case PrimaryStat.Mind: return stats.MagicAttack;
                default: return stats.SupportPower;
            }
        }

        // What the stat's power is called on the character sheet.
        public static string PowerName(PrimaryStat stat)
        {
            switch (stat)
            {
                case PrimaryStat.Body: return "Physical Attack";
                case PrimaryStat.Mind: return "Magic Attack";
                default: return "Support Power";
            }
        }

        public static float Total(UnitStats stats, IAbility ability)
        {
            if (stats == null || ability == null) return 0f;

            var weights = ability.Scaling;
            float sum = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Math.Max(0f, weights[i].Weight);
                if (w > 0f) sum += w * PowerOf(stats, weights[i].Stat);
            }
            return sum;
        }

        public static float WeightOf(IAbility ability, PrimaryStat stat)
        {
            if (ability == null) return 0f;

            var weights = ability.Scaling;
            float sum = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i].Stat == stat) sum += Math.Max(0f, weights[i].Weight);
            return sum;
        }

        public static float TotalWeight(IAbility ability)
            => WeightOf(ability, PrimaryStat.Body) + WeightOf(ability, PrimaryStat.Mind) + WeightOf(ability, PrimaryStat.Spirit);

        public static bool HasScaling(IAbility ability) => TotalWeight(ability) > 0f;

        // A Heal ability restores HP when it has flat HP OR scales off a stat.
        public static bool HealsHp(IAbility ability) => ability != null && (ability.HealHp > 0 || HasScaling(ability));

        // One line for the Inspector: "Total weight 1.20 = Body 0.60 + Spirit 0.60 (guide: pure 1.0, hybrid 1.2)".
        public static string Summarize(IAbility ability)
        {
            float total = TotalWeight(ability);
            if (total <= 0f) return "No stat scaling";

            var parts = new List<string>();
            foreach (PrimaryStat stat in Enum.GetValues(typeof(PrimaryStat)))
            {
                float w = WeightOf(ability, stat);
                if (w > 0f) parts.Add($"{stat} {w:0.00}");
            }

            var sb = new StringBuilder();
            sb.Append($"Total weight {total:0.00} = ").Append(string.Join(" + ", parts));
            sb.Append($"  (guide: pure {PureWeight:0.0}, hybrid {PureWeight * HybridWeightFactor:0.0})");
            return sb.ToString();
        }
    }
}
