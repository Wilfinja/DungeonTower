using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// Two thin IWalkableMap wrappers around the real map, each adding
    /// exactly one battlefield-object concern on top of it, so every
    /// existing caller of movement/pathing/line-of-sight code keeps
    /// working unchanged — it just gets handed a different IWalkableMap
    /// instance depending on whether it cares about walking or seeing.
    /// Construct one of each (wrapping the real map + the registry)
    /// once, alongside the registry itself, and swap them in wherever
    /// BattleController currently passes `_map`:
    ///   - MovementRangeCalculator, PathDistanceField, HandleMoveClick's
    ///     reachability checks -> MovementMapView
    ///   - every LineOfSight.HasClearPath call, and CombatUnit.CanSense
    ///     -> SightMapView
    /// Live queries against the registry, not cached — no rebuilding
    /// needed when an object is added/removed/expires.
    /// </summary>
    public sealed class MovementMapView : IWalkableMap
    {
        private readonly IWalkableMap _baseMap;
        private readonly BattlefieldObjectRegistry _registry;

        public MovementMapView(IWalkableMap baseMap, BattlefieldObjectRegistry registry)
        {
            _baseMap = baseMap;
            _registry = registry;
        }

        public bool InBounds(GridPosition position) => _baseMap.InBounds(position);

        public bool IsWalkable(GridPosition position)
            => _baseMap.IsWalkable(position) && !_registry.BlocksMovementAt(position);
    }

    public sealed class SightMapView : IWalkableMap
    {
        private readonly IWalkableMap _baseMap;
        private readonly BattlefieldObjectRegistry _registry;

        public SightMapView(IWalkableMap baseMap, BattlefieldObjectRegistry registry)
        {
            _baseMap = baseMap;
            _registry = registry;
        }

        public bool InBounds(GridPosition position) => _baseMap.InBounds(position);

        // LineOfSight.HasClearPath treats "not walkable" as "blocks
        // sight" — that's the whole trick here: a Cloud blocks sight
        // without touching MovementMapView at all, and a solid Wall
        // (which sets both flags) blocks both, each through its own view.
        public bool IsWalkable(GridPosition position)
            => _baseMap.IsWalkable(position) && !_registry.BlocksSightAt(position);
    }
}
