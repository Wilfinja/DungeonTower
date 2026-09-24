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
    /// at full health or when dead).
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
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 0.6f, 0f);

        private CombatUnit _unit;
        private Camera _worldCamera;
        private Canvas _canvas;
        private RectTransform _rect;
        private RectTransform _parentRect;

        public void Bind(CombatUnit unit)
        {
            _unit = unit;
            _rect = (RectTransform)transform;
            _parentRect = (RectTransform)transform.parent;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _worldCamera = Camera.main;
            UpdateBar();
        }

        private void LateUpdate() => UpdateBar();

        private void UpdateBar()
        {
            if (_unit == null) return;

            int max = _unit.Stats.MaxHp;
            bool show = _unit.IsAlive && max > 0 && _unit.CurrentHp < max;
            _slider.gameObject.SetActive(show);
            if (!show) return;

            _slider.maxValue = max;
            _slider.value = _unit.CurrentHp;

            var world = GridToWorld.ToWorldPosition(_unit.Position) + _worldOffset;
            Vector2 screen = _worldCamera.WorldToScreenPoint(world);
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screen, uiCamera, out var local))
                _rect.localPosition = local;
        }
    }
}
