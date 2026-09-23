using UnityEngine;
using DungeonTower.Core;
using DungeonTower.Generation;

namespace DungeonTower.UI
{
    /// <summary>
    /// Visual for a single tile. Uses a SpriteRenderer's color to
    /// distinguish floor/wall for now — swap for real tile art later
    /// without touching anything else. Needs a Collider2D on the prefab
    /// (a BoxCollider2D is enough) so BattleController can detect clicks.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private Color _floorColor = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField] private Color _wallColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _moveHighlightColor = new Color(0.25f, 0.55f, 0.85f);
        [SerializeField] private Color _attackHighlightColor = new Color(0.85f, 0.3f, 0.3f);
        [SerializeField] private Color _previewHighlightColor = new Color(0.9f, 0.75f, 0.2f);

        private SpriteRenderer _renderer;
        private TileType _type;

        public GridPosition Position { get; private set; }

        public void Initialize(GridPosition position, TileType type)
        {
            Position = position;
            _type = type;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.color = BaseColor;
        }

        // Preview is the live AoE-footprint overlay shown while hovering
        // with an area ability aimed — distinct from Attack (the static
        // set of currently-valid target tiles).
        public enum HighlightState { None, Move, Attack, Preview }

        public void SetHighlighted(HighlightState state)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.color = state switch
            {
                HighlightState.Move => _moveHighlightColor,
                HighlightState.Attack => _attackHighlightColor,
                HighlightState.Preview => _previewHighlightColor,
                _ => BaseColor
            };
        }

        private Color BaseColor => _type == TileType.Wall ? _wallColor : _floorColor;
    }
}
