namespace DungeonTower.Core
{
    /// <summary>
    /// The footprint an ability's damage lands in around the tile the
    /// player aims at. Single needs only the impact tile. Blast is a
    /// radius around the impact tile. Line and Cone also need the
    /// caster's position, to know which direction to extend in from.
    /// Ring and Cross are variations on Blast (see AreaOfEffect). Chain
    /// isn't tile geometry at all — it depends on where living units
    /// currently stand, so it's resolved by BattleController.ExecuteChain
    /// instead of AreaOfEffect; treated as Single for aiming purposes
    /// (must click directly on the first target) and as an empty/no-op
    /// shape by anything that only asks AreaOfEffect for a footprint.
    /// APPEND-ONLY — serialized by value on every AbilitySO asset.
    /// </summary>
    public enum AttackShape
    {
        Single,
        Blast,
        Line,
        Cone,
        Ring,
        Cross,
        Chain
    }
}
