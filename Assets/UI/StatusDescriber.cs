using System;
using System.Text;
using DungeonTower.Combat;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// Turns one ACTIVE status on one unit into tooltip text: what it is
    /// doing to that unit right now ("-1 to total AP", "Takes 3 damage at
    /// the start of each turn"). Numbers come from the live instance
    /// (stacks, magnitude, turns left), so the text is always current.
    /// Pure text-building, like ItemDescriber. When a status gets a new
    /// behavior, add its line to Effect().
    /// </summary>
    public static class StatusDescriber
    {
        private const string BeneficialColor = "#7BE07F";
        private const string HarmfulColor = "#FF7B7B";

        public static string Describe(CombatUnit owner, StatusEffectInstance status)
        {
            var rule = StatusRules.Get(status.Id);
            var sb = new StringBuilder();

            string tint = rule.Polarity == StatusPolarity.Beneficial ? BeneficialColor : HarmfulColor;
            sb.Append("<b><color=").Append(tint).Append('>')
              .Append(UiText.Humanize(status.Id.ToString()))
              .Append("</color></b>");

            // The headline effect.
            string effect = Effect(owner, status);
            if (!string.IsNullOrEmpty(effect)) sb.Append('\n').Append(effect);

            // Any stat changes it carries (Weaken, Sunder, Fortify, ...),
            // already multiplied by the current stacks.
            foreach (var line in ItemDescriber.BonusLines(status.EffectiveBonus))
                sb.Append('\n').Append(line);

            // How long it lasts.
            string lasting = Lasting(rule, status);
            if (!string.IsNullOrEmpty(lasting)) sb.Append("\n\n").Append(lasting);

            return sb.ToString();
        }

        private static string Effect(CombatUnit owner, StatusEffectInstance s)
        {
            switch (s.Id)
            {
                // Over time
                case StatusEffectId.Poison:
                    return $"Takes {s.Stacks} damage at the start of each turn.";
                case StatusEffectId.Burn:
                {
                    int damage = Math.Max(1,
                        (int)Math.Round(owner.Stats.MaxHp * s.Magnitude / 100.0, MidpointRounding.AwayFromZero));
                    return $"Takes {UiText.Num(s.Magnitude)}% of max HP ({damage}) at the start of each turn.";
                }
                case StatusEffectId.Bleed:
                    return $"Takes {s.Stacks} damage every time it moves.";
                case StatusEffectId.Regeneration:
                    return $"Heals {s.Stacks} HP at the start of each turn (plus any healing-received bonus).";
                case StatusEffectId.Doom:
                    return $"Takes {UiText.Num(s.Magnitude)} damage when the countdown ends. Cleanse defuses it.";

                // Action points
                case StatusEffectId.Slow:  return "-1 to total AP";
                case StatusEffectId.Haste: return "+1 to total AP";

                // Control
                case StatusEffectId.Root:    return "Cannot move.";
                case StatusEffectId.Stun:    return "Cannot act or move; its turn is skipped.";
                case StatusEffectId.Silence: return "Cannot read scrolls or use silenceable weapon abilities. Potions still work.";
                case StatusEffectId.Taunt:
                {
                    string who = s.Source != null ? s.Source.DisplayName : "whoever taunted it";
                    return $"Must target {who}.";
                }

                // Reactive
                case StatusEffectId.Mark:
                    return $"Takes {UiText.Num(s.Magnitude)}% more damage from all sources.";
                case StatusEffectId.Thorns:
                    return $"Reflects {UiText.Num(s.Magnitude)}% of damage taken back at attackers within {DamagePipeline.ThornsMaxRange} tile.";
                case StatusEffectId.Momentum:
                    return s.Primed
                        ? $"Armed: its next attack deals {UiText.Num(s.Magnitude)}% more damage."
                        : $"Moving arms it: the next attack after a move deals {UiText.Num(s.Magnitude)}% more damage.";
                case StatusEffectId.SecondWind:
                    return "Survives the next lethal hit with 1 HP.";
                case StatusEffectId.Ward:
                    return $"Absorbs the next {(int)Math.Floor(s.Magnitude)} damage.";

                // Weaken / Sunder / Fortify: the stat lines are the whole story.
                default:
                    return null;
            }
        }

        private static string Lasting(StatusRule rule, StatusEffectInstance s)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (rule.UsesDuration)
                parts.Add(s.Duration == 1 ? "1 turn left" : $"{s.Duration} turns left");

            if (rule.StackDecayPerTurn > 0)
                parts.Add($"loses {rule.StackDecayPerTurn} stack{(rule.StackDecayPerTurn == 1 ? "" : "s")} each turn");

            if (rule.MagnitudeDecayPerTurn > 0f)
                parts.Add($"weakens by {UiText.Num(rule.MagnitudeDecayPerTurn)} each turn");

            if (parts.Count == 0) return "Lasts until used up or removed.";

            string text = string.Join("; ", parts);
            return char.ToUpper(text[0]) + text.Substring(1) + ".";
        }
    }
}
