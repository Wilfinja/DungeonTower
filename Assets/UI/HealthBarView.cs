using UnityEngine;
using UnityEngine.UI;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Health bar that lives on the regular screen-space Canvas and
    /// follows one CombatUnit around the battlefield. Every frame it
    /// reads the unit's HP and position, converts the position from
    /// world space to canvas space, and shows/hides the Slider (hidden
    /// at full health, when dead, or when fog-hidden).
    ///
    /// The optional status row hangs below the bar and is independent of
    /// the Slider's visibility — statuses show even at full HP. It is
    /// hidden when the unit is dead or fog-hidden. That means the root
    /// now follows the unit whenever it's alive, not only while the
    /// Slider is showing.
    ///
    /// The root object must stay active so LateUpdate keeps running —
    /// only the child Slider is toggled. Runs after CameraFollow (which
    /// defaults to order 0) so the bar doesn't lag a frame behind a
    /// moving camera.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private StatusRowView _statusRow; // optional
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.6f, 0f);

        private CombatUnit _unit;
        private Camera _worldCamera;
        private Canvas _canvas;
        private RectTransform _rect;
        private RectTransform _parentRect;
        private bool _fogVisible = true;
        private Transform _follow;

        public void Bind(CombatUnit unit, Transform follow)
        {
            _follow = follow;
            _unit = unit;
            _rect = (RectTransform)transform;
            _parentRect = (RectTransform)transform.parent;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _worldCamera = Camera.main;
            UpdateBar();
        }

        // Defaults to true, so a scene that never calls this behaves
        // exactly as before fog existed.
        public void SetFogVisible(bool visible) => _fogVisible = visible;

        private void LateUpdate() => UpdateBar();

        private void UpdateBar()
        {
            if (_unit == null) return;

            bool alive = _unit.IsAlive;
            int max = _unit.Stats.MaxHp;

            bool showBar = alive && _fogVisible && max > 0 && _unit.CurrentHp < max;
            _slider.gameObject.SetActive(showBar);
            if (showBar)
            {
                _slider.maxValue = max;
                _slider.value = _unit.CurrentHp;
            }

            if (_statusRow != null)
                _statusRow.Refresh(alive && _fogVisible ? _unit : null);

            if (!alive) return;

            var basePos = _follow != null ? _follow.position : GridToWorld.ToWorldPosition(_unit.Position);
            var world = basePos + _worldOffset;
            Vector2 screen = _worldCamera.WorldToScreenPoint(world);
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screen, uiCamera, out var local))
                _rect.localPosition = local;
        }
    }
}
