namespace DungeonTower.Core
{
    /// <summary>
    /// The minimal read-only view Combat needs of the map that Generation
    /// builds. Living in Core lets Combat query tile walkability without
    /// referencing the Generation assembly at all — Generation will
    /// implement this interface once its map type exists.
    /// </summary>
    public interface IWalkableMap
    {
        bool InBounds(GridPosition position);
        bool IsWalkable(GridPosition position);
    }
}
