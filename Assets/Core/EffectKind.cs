namespace DungeonTower.Core
{
    /// <summary>
    /// What an ability actually does once it reaches a target — separate
    /// from AttackKind, which only matters for a Damage effect (whether
    /// it scales off Physical or Magic Attack). Damage is the default,
    /// so every ability authored before this existed keeps behaving
    /// exactly as it did.
    /// </summary>
    public enum EffectKind
    {
        Damage,
        Heal,
        Buff
    }
}
