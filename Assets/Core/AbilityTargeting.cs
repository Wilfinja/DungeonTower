namespace DungeonTower.Core
{
    /// <summary>
    /// Who an ability is aimed at. Damage hits enemies; Heal/Buff/Cleanse
    /// help allies; a Status ability follows the polarity of what it applies —
    /// all-Beneficial statuses target allies, anything containing a
    /// Harmful one targets enemies (a mixed list counts as hostile, so
    /// author beneficial and harmful statuses as separate abilities).
    /// Lives in Core so BattleController's targeting, its AoE
    /// caster-exclusion rule, and (later) enemy AI all agree.
    /// </summary>
    public static class AbilityTargeting
    {
        public static bool TargetsAllies(IAbility ability)
        {
            switch (ability.EffectKind)
            {
                case EffectKind.Damage:
                    return false;
                case EffectKind.Status:
                    return AllBeneficial(ability);
                default: // Heal, Buff, Cleanse
                    return true;
            }
        }

        /// <summary>
        /// Range 0 means Self: the caster's own tile is the only legal aim
        /// point. Area shapes still spread out from there (a Blast with
        /// radius 2 is a nova around the caster). Not meaningful for Summon
        /// (needs an empty tile) or for Line/Cone/Sweep (need a direction).
        /// </summary>
        public static bool IsSelfCast(IAbility ability) => ability.Range <= 0;

        /// <summary>
        /// True if `ability` may affect a unit on the caster's side
        /// (sameSide, the caster included) or on the other side.
        /// </summary>
        public static bool Affects(IAbility ability, bool sameSide)
            => sameSide ? !ability.IgnoreAllies : !ability.IgnoreEnemies;

        private static bool AllBeneficial(IAbility ability)
        {
            var statuses = ability.Statuses;
            if (statuses == null || statuses.Count == 0) return false;

            for (int i = 0; i < statuses.Count; i++)
                if (StatusRules.Get(statuses[i].Id).Polarity == StatusPolarity.Harmful)
                    return false;
            return true;
        }
    }
}
