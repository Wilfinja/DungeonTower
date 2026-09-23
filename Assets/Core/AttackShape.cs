namespace DungeonTower.Core
{
    /// <summary>
    /// The footprint an ability's damage lands in around the tile the
    /// player aims at. Single needs only the impact tile. Blast is a
    /// radius around the impact tile. Line and Cone also need the
    /// caster's position, to know which direction to extend in from —
    /// resolved by whatever computes affected tiles once GridPosition's
    /// exact shape is confirmed (see follow-up request).
    /// </summary>
    public enum AttackShape
    {
        Single,
        Blast,
        Line,
        Cone
    }
}
