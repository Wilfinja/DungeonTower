using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// The hover text for Body / Mind / Spirit. Every number is read from
    /// DerivedStatFormulas, so retuning a constant there updates the
    /// tooltips automatically — nothing here to keep in sync.
    /// </summary>
    public static class StatDescriptions
    {
        // `unit` is optional; when given, the tooltip also shows what the
        // hero's CURRENT points in this stat are worth.
        public static string Describe(PrimaryStat stat, UnitStats unit)
        {
            int points = unit != null ? unit.Current.Get(stat) : 0;
            bool showCurrent = unit != null;

            switch (stat)
            {
                case PrimaryStat.Body:
                    return "<b>Body</b>\nToughness and raw strength. Powers martial (physical) abilities.\n"
                        + $"+{UiText.Num(DerivedStatFormulas.HpPerBody)} Max HP per point\n"
                        + $"+{UiText.Num(DerivedStatFormulas.PhysicalAttackPerBody)} Physical Attack per point\n"
                        + $"+{UiText.Num(DerivedStatFormulas.PhysicalDefensePerBody)} Physical Defense per point\n"
                        + $"+{UiText.Num(DerivedStatFormulas.HealingReceivedPerBody)}% healing received per point\n"
                        + $"{UiText.Num(DerivedStatFormulas.InitiativeWeightBody * 100f)}% of Initiative"
                        + (showCurrent
                            ? $"\n<i>Right now: {UiText.Num(points * DerivedStatFormulas.HpPerBody)} HP, "
                              + $"{UiText.Num(points * DerivedStatFormulas.PhysicalAttackPerBody)} Physical Attack, "
                              + $"{UiText.Num(points * DerivedStatFormulas.PhysicalDefensePerBody)} Physical Defense, "
                              + $"+{UiText.Num(points * DerivedStatFormulas.HealingReceivedPerBody)}% healing received</i>"
                            : "")
                        + "\nSome weapons and armor require a minimum Body.";

                case PrimaryStat.Mind:
                    return "<b>Mind</b>\nMagical power and focus. Powers magic abilities.\n"
                        + $"+{UiText.Num(DerivedStatFormulas.MpPerMind)} Max MP per point\n"
                        + $"+{UiText.Num(DerivedStatFormulas.MagicAttackPerMind)} Magic Attack per point\n"
                        + $"+{UiText.Num(DerivedStatFormulas.MagicDefensePerMind)} Magic Defense per point "
                        + $"(never below {UiText.Num(DerivedStatFormulas.MagicDefenseFloor)})\n"
                        + $"{UiText.Num(DerivedStatFormulas.InitiativeWeightMind * 100f)}% of Initiative"
                        + (showCurrent
                            ? $"\n<i>Right now: {UiText.Num(points * DerivedStatFormulas.MpPerMind)} MP, "
                              + $"{UiText.Num(points * DerivedStatFormulas.MagicAttackPerMind)} Magic Attack, "
                              + $"{UiText.Num(System.Math.Max(DerivedStatFormulas.MagicDefenseFloor, points * DerivedStatFormulas.MagicDefensePerMind))} Magic Defense</i>"
                            : "")
                        + "\nSome weapons, armor, and scrolls require a minimum Mind.";

                case PrimaryStat.Spirit:
                    return "<b>Spirit</b>\nWillpower and influence. Powers support abilities and statuses.\n"
                        + $"+{UiText.Num(DerivedStatFormulas.StatusResistPerSpirit)}% Status Resist per point "
                        + $"(max {UiText.Num(DerivedStatFormulas.StatusResistCap)}% from Spirit)\n"
                        + $"+{UiText.Num(DerivedStatFormulas.PotencyPerSpirit)}% Potency per point "
                        + "(more likely to land statuses, and they hit harder)\n"
                        + $"+{UiText.Num(DerivedStatFormulas.SupportPowerPerSpirit)} Support Power per point (healing)\n"
                        + $"+1 MP per turn in combat for every {UiText.Num(1f / DerivedStatFormulas.MpRegenPerSpirit)} points\n"
                        + $"{UiText.Num(DerivedStatFormulas.InitiativeWeightSpirit * 100f)}% of Initiative"
                        + (showCurrent
                            ? $"\n<i>Right now: {UiText.Num(System.Math.Min(DerivedStatFormulas.StatusResistCap, points * DerivedStatFormulas.StatusResistPerSpirit))}% Status Resist, "
                              + $"{UiText.Num(points * DerivedStatFormulas.PotencyPerSpirit)}% Potency, "
                              + $"{UiText.Num(points * DerivedStatFormulas.SupportPowerPerSpirit)} Support Power, "
                              + $"{UiText.Num(points * DerivedStatFormulas.MpRegenPerSpirit)} MP regen</i>"
                            : "")
                        + "\nSome weapons and armor require a minimum Spirit.";

                default:
                    return stat.ToString();
            }
        }
    }
}
