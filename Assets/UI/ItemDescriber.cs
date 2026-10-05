using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DungeonTower.Combat;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// Turns a weapon, armor, or single ability into readable rich text:
    /// damage, single-target vs area, stat bonuses, statuses with their
    /// chances, cooldown, and stat requirements. Pure text-building with
    /// no UI references, so the character sheet uses it inline today and
    /// inventory/loot tooltips can call the same methods later.
    ///
    /// Pass the viewing unit's UnitStats to get live numbers (damage
    /// worked out from THEIR attack and crit, requirements turning red
    /// when THEY don't meet them); pass null for generic, unit-less text.
    /// </summary>
    public static class ItemDescriber
    {
        public static string Describe(IWeapon weapon, UnitStats viewer)
        {
            if (weapon == null) return "";

            var sb = new StringBuilder();
            sb.Append("<b>").Append(weapon.Name).Append("</b>");
            sb.Append('\n').Append(RequirementLine(weapon.RequiredStat, weapon.RequiredStatValue, viewer));

            var abilities = weapon.Abilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                sb.Append("\n\n");
                sb.Append(Describe(abilities[i], viewer, abilities.Count > 1 ? $"{i + 1}. " : ""));
            }
            return sb.ToString();
        }

        public static string Describe(IArmor armor, UnitStats viewer)
        {
            if (armor == null) return "";

            var sb = new StringBuilder();
            sb.Append("<b>").Append(armor.Name).Append("</b>");
            sb.Append('\n').Append(RequirementLine(armor.RequiredStat, armor.RequiredStatValue, viewer));

            var lines = BonusLines(armor.PassiveBonus);
            sb.Append('\n');
            sb.Append(lines.Count > 0 ? "Grants: " + string.Join(", ", lines) : "No stat bonuses");
            return sb.ToString();
        }

        public static string Describe(IAbility ability, UnitStats viewer, string titlePrefix = "")
        {
            if (ability == null) return "";

            var sb = new StringBuilder();
            sb.Append("<b>").Append(titlePrefix).Append(ability.Name).Append("</b>");

            switch (ability.EffectKind)
            {
                case EffectKind.Damage:
                    AppendDamage(sb, ability, viewer);
                    break;

                case EffectKind.Heal:
                    if (ability.HealHp > 0) sb.Append("\nRestores ").Append(ability.HealHp).Append(" HP");
                    if (ability.HealMp > 0) sb.Append("\nRestores ").Append(ability.HealMp).Append(" MP");
                    break;

                case EffectKind.Buff:
                {
                    var lines = BonusLines(ability.Bonus);
                    sb.Append("\nGrants for this battle: ");
                    sb.Append(lines.Count > 0 ? string.Join(", ", lines) : "nothing");
                    break;
                }

                case EffectKind.Cleanse:
                {
                    var cleanses = ability.Cleanses;
                    if (cleanses == null || cleanses.Count == 0)
                        sb.Append("\nRemoves every harmful status");
                    else
                    {
                        var names = new List<string>();
                        foreach (var id in cleanses) names.Add(UiText.Humanize(id.ToString()));
                        sb.Append("\nRemoves: ").Append(string.Join(", ", names));
                    }
                    break;
                }

                case EffectKind.Summon:
                    AppendSummon(sb, ability);
                    break;

                case EffectKind.Status:
                    // The statuses ARE the ability — listed below.
                    break;
            }

            AppendTargeting(sb, ability, viewer);
            AppendMovement(sb, ability);

            var statuses = ability.Statuses;
            if (statuses != null && statuses.Count > 0)
            {
                string lead = ability.EffectKind == EffectKind.Status ? "Applies" : "On hit";
                foreach (var app in statuses)
                    sb.Append('\n').Append(lead).Append(": ").Append(DescribeStatus(app));
            }

            return sb.ToString();
        }

        // "Requires Body 6" — red when the viewer falls short.
        public static string RequirementLine(PrimaryStat stat, int value, UnitStats viewer)
        {
            if (value <= 0) return "No stat requirement";

            string text = $"Requires {stat} {value}";
            bool unmet = viewer != null && viewer.Current.Get(stat) < value;
            return unmet ? UiText.Unmet(text) : text;
        }

        // Every non-zero field, signed: "+8 Max HP", "+3 Physical Defense".
        public static List<string> BonusLines(DerivedStatBonus b)
        {
            var lines = new List<string>();
            AddBonus(lines, b.Hp, "Max HP");
            AddBonus(lines, b.Mp, "Max MP");
            AddBonus(lines, b.PhysicalAttack, "Physical Attack");
            AddBonus(lines, b.PhysicalDefense, "Physical Defense");
            AddBonus(lines, b.MagicAttack, "Magic Attack");
            AddBonus(lines, b.MagicDefense, "Magic Defense");
            AddBonus(lines, b.Initiative, "Initiative");
            AddBonus(lines, b.MoveRange, "Move Range");
            AddBonus(lines, b.StatusResist, "Status Resist", "%");
            AddBonus(lines, b.CritChance, "Crit Chance", "%");
            return lines;
        }

        private static void AddBonus(List<string> lines, float value, string label, string suffix = "")
        {
            if (Mathf.Abs(value) < 0.0001f) return;
            lines.Add($"{UiText.Signed(value)}{suffix} {label}");
        }

        private static void AppendDamage(StringBuilder sb, IAbility ability, UnitStats viewer)
        {
            bool physical = ability.Kind == AttackKind.Physical;
            string kind = physical ? "Physical" : "Magic";

            if (viewer != null)
            {
                float attack = physical ? viewer.PhysicalAttack : viewer.MagicAttack;
                float raw = attack * ability.DamageMultiplier;
                sb.Append($"\nDeals about {UiText.Num(raw)} {kind} damage ")
                  .Append($"({UiText.Num(attack)} attack × {UiText.Num(ability.DamageMultiplier)}), minus the target's defense");
                sb.Append($"\nCrit chance {UiText.Num(viewer.CritChance)}% for ×{UiText.Num(AttackResolver.CritDamageMultiplier)} damage");
            }
            else
            {
                sb.Append($"\nDeals {UiText.Num(ability.DamageMultiplier)}× your {kind} Attack as {kind} damage, minus the target's defense");
            }
        }

        // "Range 3 · Enemies · Single target · Cooldown 2 turns"
        private static void AppendTargeting(StringBuilder sb, IAbility ability, UnitStats viewer)
        {
            var parts = new List<string> { $"Range {ability.Range}" };

            // Red when the viewer's MAX MP is below the cost — they could never cast it.
            if (ability.MpCost > 0)
            {
                string cost = $"MP cost {ability.MpCost}";
                parts.Add(viewer != null && viewer.MaxMp < ability.MpCost ? UiText.Unmet(cost) : cost);
            }

            // Summons are placed on the ground, not aimed at a unit type.
            if (ability.EffectKind != EffectKind.Summon)
                parts.Add(AbilityTargeting.TargetsAllies(ability) ? "Allies" : "Enemies");

            parts.Add(ShapeText(ability));

            if (ability.CooldownTurns > 0)
                parts.Add($"Cooldown {ability.CooldownTurns} turn{(ability.CooldownTurns == 1 ? "" : "s")}");

            sb.Append('\n').Append(string.Join(" · ", parts));
        }

        private static string ShapeText(IAbility ability)
        {
            int r = ability.AreaRadius;
            switch (ability.AreaShape)
            {
                case AttackShape.Single: return "Single target";
                case AttackShape.Blast: return $"Area: blast, radius {r}";
                case AttackShape.Ring: return $"Area: ring, radius {r}";
                case AttackShape.Cross: return $"Area: cross, reach {r}";
                case AttackShape.Line: return $"Area: line, size {r}";
                case AttackShape.Cone: return $"Area: cone, size {r}";
                case AttackShape.Chain:
                    return $"Chain: hits the target, then jumps up to {r} more time{(r == 1 ? "" : "s")} "
                         + $"(up to {ability.Range} tiles each)";
                default: return "Area";
            }
        }

        private static void AppendMovement(StringBuilder sb, IAbility ability)
        {
            if (ability.PushDistance > 0)
                sb.Append($"\nPushes the target {ability.PushDistance} tile{(ability.PushDistance == 1 ? "" : "s")} away");
            else if (ability.PushDistance < 0)
                sb.Append($"\nPulls the target {-ability.PushDistance} tile{(ability.PushDistance == -1 ? "" : "s")} closer");

            if (ability.SwapWithCaster) sb.Append("\nSwaps places with the target");
        }

        private static void AppendSummon(StringBuilder sb, IAbility ability)
        {
            switch (ability.SummonTriggerMode)
            {
                case SummonTriggerMode.OnEntry:
                    sb.Append("\nTriggers when a unit steps onto it")
                      .Append(ability.SummonConsumedAfterTrigger ? " (once)" : " (every time)");
                    if (ability.SummonIsHidden) sb.Append(", hidden until triggered");
                    break;
                case SummonTriggerMode.OnRoundTick:
                    sb.Append(ability.SummonAffectsAllies
                        ? "\nEach round, affects allies nearby"
                        : "\nEach round, affects enemies nearby");
                    if (ability.AuraRadius > 0) sb.Append($" (out to {ability.AuraRadius} tiles beyond its edge)");
                    break;
                default:
                    sb.Append("\nA placed obstacle");
                    break;
            }

            if (ability.SummonBlocksMovement) sb.Append("\nBlocks movement");
            if (ability.SummonBlocksLineOfSight) sb.Append("\nBlocks line of sight");
            if (ability.SummonDuration > 0) sb.Append($"\nLasts {ability.SummonDuration} round{(ability.SummonDuration == 1 ? "" : "s")}");
            if (ability.SummonMaxHp > 0) sb.Append($"\nCan be destroyed ({ability.SummonMaxHp} HP)");
            if (ability.EndsIfOwnerMoves) sb.Append("\nEnds if the caster moves");
        }

        // "30% chance: Poison (2 stacks; damage each turn equal to stacks)"
        private static string DescribeStatus(StatusApplication app)
        {
            var rule = StatusRules.Get(app.Id);
            var details = new List<string>();

            if (rule.Reapply == ReapplyMode.AddStacks || rule.StackDecayPerTurn > 0)
                details.Add($"{app.EffectiveStacks} stack{(app.EffectiveStacks == 1 ? "" : "s")}");

            if (app.Magnitude > 0f)
            {
                switch (app.Id)
                {
                    case StatusEffectId.Burn: details.Add($"{UiText.Num(app.Magnitude)}% of max HP per turn"); break;
                    case StatusEffectId.Doom: details.Add($"{UiText.Num(app.Magnitude)} damage when it detonates"); break;
                    case StatusEffectId.Ward: details.Add($"absorbs {UiText.Num(app.Magnitude)} damage"); break;
                    default: details.Add($"strength {UiText.Num(app.Magnitude)}"); break;
                }
            }

            if (rule.UsesDuration)
            {
                int turns = app.EffectiveDuration(rule);
                details.Add($"{turns} turn{(turns == 1 ? "" : "s")}");
            }

            var bonus = BonusLines(app.StatBonus);
            if (bonus.Count > 0) details.Add((app.EffectiveStacks > 1 || rule.Reapply == ReapplyMode.AddStacks ? "per stack: " : "")
                + string.Join(", ", bonus));

            string hint = Hint(app.Id);
            if (hint != null) details.Add(hint);

            string chance = app.EffectiveChance >= 100f ? "" : $"{UiText.Num(app.EffectiveChance)}% chance: ";
            string name = UiText.Humanize(app.Id.ToString());
            return details.Count > 0 ? $"{chance}{name} ({string.Join("; ", details)})" : $"{chance}{name}";
        }

        // Only statuses whose behavior is spelled out in StatusRules get a hint.
        private static string Hint(StatusEffectId id)
        {
            switch (id)
            {
                case StatusEffectId.Poison: return "damage each turn equal to stacks";
                case StatusEffectId.Regeneration: return "heals each turn equal to stacks";
                case StatusEffectId.Bleed: return "damage each time the target moves";
                case StatusEffectId.Root: return "can't move";
                case StatusEffectId.Stun: return "loses its turn";
                case StatusEffectId.Silence: return "can't use scrolls or silenceable abilities";
                case StatusEffectId.SecondWind: return "survives one lethal hit at 1 HP";
                default: return null;
            }
        }
    }
}
