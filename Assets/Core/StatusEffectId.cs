namespace DungeonTower.Core
{
    /// <summary>
    /// Every status effect the game knows about. APPEND-ONLY: AbilitySO
    /// assets serialize this enum by integer value, so reordering or
    /// inserting in the middle would silently re-point existing assets at
    /// the wrong status. Add new ids at the bottom.
    ///
    /// Each id's behavior is data-driven by StatusRules (how it stacks,
    /// decays, and expires) plus, for the ones that do something beyond
    /// modifying stats, an IStatusBehavior registered in StatusBehaviors.
    /// Weaken/Sunder/Slow/Haste/Fortify need no behavior at all — they're
    /// just a timed DerivedStatBonus authored on the ability.
    /// </summary>
    public enum StatusEffectId
    {
        // Damage / heal over time
        Poison,
        Burn,
        Bleed,
        Regeneration,
        Doom,

        // Timed stat modifiers (pure data — no behavior needed)
        Weaken,
        Sunder,
        Slow,
        Haste,
        Fortify,

        // Control
        Root,
        Stun,
        Silence,
        Taunt,

        // Reactive / flag-style
        Mark,
        Thorns,
        Momentum,
        SecondWind,
        Ward
    }
}
