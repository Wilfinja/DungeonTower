using DungeonTower.Core;

namespace DungeonTower.Combat
{
    // Where an ability being used comes from — Silence treats each differently.
    public enum AbilitySource
    {
        Weapon,
        Scroll,
        Potion
    }

    /// <summary>
    /// What Silence blocks: scrolls always (reading one is casting),
    /// potions never (drinking isn't), and equipped-weapon abilities only
    /// if that ability is flagged Silenceable (a fireball staff yes, a
    /// sword swing no).
    /// </summary>
    public static class SilenceRules
    {
        public static bool IsBlocked(CombatUnit unit, IAbility ability, AbilitySource source)
        {
            if (ability == null || !unit.Status.Has(StatusEffectId.Silence)) return false;

            switch (source)
            {
                case AbilitySource.Scroll: return true;
                case AbilitySource.Weapon: return ability.Silenceable;
                default: return false;
            }
        }
    }
}
