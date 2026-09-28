using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// What the player currently sees, and has ever seen. A tile that's
    /// been seen once STAYS Explored forever after (Fire-Emblem-style
    /// memory — the map shape doesn't re-hide), while CurrentlyVisible
    /// is recomputed from scratch every call: only living units count as
    /// vision sources, each seeing out to its own DetectionRadius,
    /// LOS-gated through the same SightMapView a Wall/Cloud already
    /// blocks enemy vision through (see Phase 5) — so an obscuring cloud
    /// now blinds the player's own view too, not just the enemy's.
    ///
    /// DetectionRadius is reused here rather than adding a dedicated
    /// vision field: for an enemy it's still "how far it can notice
    /// you," and now it doubles as "how far it can currently see" for
    /// fog purposes — the same underlying concept either way. A player
    /// unit gets whatever value its constructor was given (see
    /// BattleController's _playerVisionRadius).
    ///
    /// One instance is enough for the player's side; nothing currently
    /// needs an enemy-side fog (enemies already query _sightMap and
    /// their own DetectionRadius directly wherever their behavior
    /// depends on seeing something — see Phase 4/5).
    /// </summary>
    public sealed class FogOfWar
    {
        private readonly HashSet<GridPosition> _explored = new HashSet<GridPosition>();
        private HashSet<GridPosition> _currentlyVisible = new HashSet<GridPosition>();

        public bool IsExplored(GridPosition pos) => _explored.Contains(pos);
        public bool IsCurrentlyVisible(GridPosition pos) => _currentlyVisible.Contains(pos);

        // Call after anything that could change what's visible: a
        // source unit moves or dies, or a Wall/Cloud is placed,
        // destroyed, or expires. Cheap enough at dungeon-tower map
        // sizes to just recompute wholesale rather than track
        // incrementally — that also sidesteps any risk of the visible
        // set drifting out of sync after some edge case.
        public void Recompute(IEnumerable<CombatUnit> sources, IWalkableMap sightMap, IWalkableMap boundsMap)
        {
            var visible = new HashSet<GridPosition>();
            foreach (var unit in sources)
            {
                if (!unit.IsAlive) continue;
                foreach (var tile in TilesWithinRadius(unit.Position, unit.DetectionRadius))
                    if (boundsMap.InBounds(tile) && LineOfSight.HasClearPath(unit.Position, tile, sightMap))
                        visible.Add(tile);
            }
            _currentlyVisible = visible;
            _explored.UnionWith(visible);
        }

        private static IEnumerable<GridPosition> TilesWithinRadius(GridPosition center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int remaining = radius - System.Math.Abs(dx);
                for (int dy = -remaining; dy <= remaining; dy++)
                    yield return new GridPosition(center.X + dx, center.Y + dy);
            }
        }
    }
}
