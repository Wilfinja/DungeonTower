namespace DungeonTower.Core
{
    /// <summary>
    /// One thing a weapon (or scroll) lets its wielder do in a fight: an
    /// attack kind, a range, a damage multiplier applied on top of the
    /// wielder's raw Physical or Magic Attack, and an area footprint.
    /// AreaShape.Single with AreaRadius 0 is a normal single-target hit —
    /// the existing Slash/Bolt/Quickshot abilities all stay exactly as
    /// they were, just with these two new properties defaulting to that.
    /// Implemented by AbilitySO (DungeonTower.Items) — Core never
    /// references ScriptableObject directly, so this stays engine-free.
    /// </summary>
    public interface IAbility
    {
        string Name { get; }
        AttackKind Kind { get; }
        int Range { get; }
        float DamageMultiplier { get; }
        AttackShape AreaShape { get; }
        int AreaRadius { get; }
    }
}
