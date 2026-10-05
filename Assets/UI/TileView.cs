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
    ///
    /// Fog and the Move/Attack/Preview highlight are independent layers
    /// that both feed the same renderer: SetFog picks Hidden (solid
    /// black, no highlight shows through — there's nothing to highlight
    /// on a tile you can't see), Explored (the normal/highlight color,
    /// darkened — remembered but not currently lit), or Visible (full
    /// color, exactly as before fog existed). Fog defaults to Visible so
    /// any scene that never calls SetFog renders exactly as it always did.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private Color _floorColor = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField] private Color _wallColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _moveHighlightColor = new Color(0.25f, 0.55f, 0.85f);
        [SerializeField] private Color _attackHighlightColor = new Color(0.85f, 0.3f, 0.3f);
        [SerializeField] private Color _previewHighlightColor = new Color(0.9f, 0.75f, 0.2f);
        [SerializeField] private Color _hiddenColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _exploredDarken = 0.35f;
        [SerializeField] private Color _doorClosedColor = new Color(0.45f, 0.30f, 0.15f);
        [SerializeField] private Color _doorOpenColor = new Color(0.28f, 0.19f, 0.10f);
        [SerializeField] private Color _exitColor = new Color(0.20f, 0.70f, 0.40f);

        private SpriteRenderer _renderer;
        private TileType _type;
        private bool _doorOpen;
        private HighlightState _highlightState = HighlightState.None;
        private FogState _fogState = FogState.Visible;

        public GridPosition Position { get; private set; }

        public void Initialize(GridPosition position, TileType type, bool doorOpen = false)
        {
            Position = position;
            _type = type;
            _doorOpen = doorOpen;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            ApplyColor();
        }

        // Preview is the live AoE-footprint overlay shown while hovering
        // with an area ability aimed — distinct from Attack (the static
        // set of currently-valid target tiles).
        public enum HighlightState { None, Move, Attack, Preview }

        // Hidden = never explored (solid black). Explored = seen before,
        // not currently in view (darkened). Visible = currently lit.
        public enum FogState { Hidden, Explored, Visible }

        public void SetHighlighted(HighlightState state)
        {
            _highlightState = state;
            ApplyColor();
        }

        public void SetDoorOpen(bool open)
        {
            _doorOpen = open;
            ApplyColor();
        }

        public void SetFog(FogState state)
        {
            _fogState = state;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();

            if (_fogState == FogState.Hidden)
            {
                _renderer.color = _hiddenColor;
                return;
            }

            var color = _highlightState switch
            {
                HighlightState.Move => _moveHighlightColor,
                HighlightState.Attack => _attackHighlightColor,
                HighlightState.Preview => _previewHighlightColor,
                _ => BaseColor
            };

            if (_fogState == FogState.Explored)
                color = new Color(color.r * _exploredDarken, color.g * _exploredDarken, color.b * _exploredDarken, color.a);

            _renderer.color = color;
        }

        private Color BaseColor
        {
            get
            {
                switch (_type)
                {
                    case TileType.Wall: return _wallColor;
                    case TileType.Door: return _doorOpen ? _doorOpenColor : _doorClosedColor;
                    case TileType.Exit: return _exitColor;
                    default: return _floorColor;
                }
            }
        }
    }
}
