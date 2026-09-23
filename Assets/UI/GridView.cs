using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonTower.Core;
using DungeonTower.Generation;

namespace DungeonTower.UI
{
    /// <summary>
    /// Spawns one TileView per tile in a DungeonMap. Assign a prefab with
    /// a SpriteRenderer, a Collider2D, and TileView attached.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;

        private readonly Dictionary<GridPosition, TileView> _tiles = new Dictionary<GridPosition, TileView>();

        // Remembered so SetPreviewOverlay/ClearPreviewOverlay can restore
        // the base Move/Attack highlight without the caller having to
        // resupply it every frame while hovering.
        private IEnumerable<GridPosition> _lastMovePositions = Enumerable.Empty<GridPosition>();
        private IEnumerable<GridPosition> _lastAttackPositions = Enumerable.Empty<GridPosition>();

        public void BuildFrom(DungeonMap map)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    var tile = Instantiate(_tilePrefab, transform);
                    tile.transform.position = GridToWorld.ToWorldPosition(pos);
                    tile.Initialize(pos, map.GetTile(pos));
                    _tiles[pos] = tile;
                }
            }
        }

        public void SetHighlights(IEnumerable<GridPosition> movePositions, IEnumerable<GridPosition> attackPositions)
        {
            _lastMovePositions = movePositions ?? Enumerable.Empty<GridPosition>();
            _lastAttackPositions = attackPositions ?? Enumerable.Empty<GridPosition>();
            ApplyBaseHighlights();
        }

        // Temporarily layers the AoE footprint preview on top of whatever
        // base highlight is currently showing — call again each frame
        // while hovering, and ClearPreviewOverlay (or SetHighlights)
        // once the hover ends.
        public void SetPreviewOverlay(IEnumerable<GridPosition> previewPositions)
        {
            ApplyBaseHighlights();
            foreach (var pos in previewPositions)
                if (_tiles.TryGetValue(pos, out var tile))
                    tile.SetHighlighted(TileView.HighlightState.Preview);
        }

        public void ClearPreviewOverlay() => ApplyBaseHighlights();

        public void ClearHighlights()
        {
            _lastMovePositions = Enumerable.Empty<GridPosition>();
            _lastAttackPositions = Enumerable.Empty<GridPosition>();
            foreach (var tile in _tiles.Values)
                tile.SetHighlighted(TileView.HighlightState.None);
        }

        private void ApplyBaseHighlights()
        {
            foreach (var tile in _tiles.Values)
                tile.SetHighlighted(TileView.HighlightState.None);
            foreach (var pos in _lastMovePositions)
                if (_tiles.TryGetValue(pos, out var tile))
                    tile.SetHighlighted(TileView.HighlightState.Move);
            foreach (var pos in _lastAttackPositions)
                if (_tiles.TryGetValue(pos, out var tile))
                    tile.SetHighlighted(TileView.HighlightState.Attack);
        }
    }
}
