namespace DungeonTower.Core
{
    /// <summary>
    /// What an ability actually does once it reaches a target — separate
    /// from AttackKind, which only matters for a Damage effect (whether
    /// it scales off Physical or Magic Attack). Damage is the default,
    /// so every ability authored before this existed keeps behaving
    /// exactly as it did.
    ///
    /// Status has no primary effect of its own — it exists purely to
    /// carry the ability's Statuses list (a poison dart, a stun, a
    /// haste scroll). Any other kind can ALSO carry Statuses as on-hit
    /// procs. APPEND-ONLY, like StatusEffectId — serialized by value.
    /// </summary>
    public enum EffectKind
    {
        Damage,
        Heal,
        Buff,
        Status
    }
}
