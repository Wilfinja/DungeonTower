namespace DungeonTower.Core
{
    /// <summary>
    /// Optional companion to IWalkableMap for maps that have tiles you can
    /// walk through but not see through (closed doors). Lives in Core for
    /// the same reason IWalkableMap does: Combat can use it without
    /// referencing the Generation assembly.
    /// </summary>
    public interface ISightBlockingMap
    {
        bool BlocksSight(GridPosition position);
    }
}
