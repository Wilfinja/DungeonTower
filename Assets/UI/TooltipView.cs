using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace DungeonTower.UI
{
    /// <summary>
    /// The one shared tooltip box. Anything can call
    /// TooltipView.Instance.Show(text) (usually through a TooltipTrigger);
    /// the box sizes itself to the text, follows the mouse, flips to stay
    /// on screen, and never blocks clicks.
    ///
    /// Setup: put THIS component on an always-active object under your
    /// main Canvas (it must not start disabled, or Instance is never set).
    /// _root is a child panel with a background Image and a TextMeshPro
    /// child for _text (word wrapping on). Visibility is a CanvasGroup
    /// alpha, so nothing here ever gets deactivated.
    /// </summary>
    public sealed class TooltipView : MonoBehaviour
    {
        public static TooltipView Instance { get; private set; }

        [SerializeField] private RectTransform _root;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField, Min(100f)] private float _maxTextWidth = 320f;
        [SerializeField, Min(0f)] private float _padding = 10f;
        [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -16f);

        private CanvasGroup _group;
        private Canvas _canvas;
        private RectTransform _parentRect;
        private bool _visible;
        private readonly Vector3[] _corners = new Vector3[4];

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("More than one TooltipView in the scene — only the newest one is used.", this);
            Instance = this;

            if (_root == null || _text == null)
            {
                Debug.LogError("TooltipView needs both _root and _text assigned.", this);
                enabled = false;
                return;
            }

            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas = _canvas.rootCanvas;
            _parentRect = _root.parent as RectTransform;

            // One anchor point + top-left pivot: sizeDelta is then the real
            // size and the root's position is its top-left corner.
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0f, 1f);

            var textRect = _text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(_padding, _padding);
            textRect.offsetMax = new Vector2(-_padding, -_padding);
            _text.raycastTarget = false;

            _group = _root.GetComponent<CanvasGroup>();
            if (_group == null) _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(string text)
        {
            if (!enabled) return;
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            _text.text = text;
            Vector2 preferred = _text.GetPreferredValues(text, _maxTextWidth, 0f);
            float width = Mathf.Min(preferred.x, _maxTextWidth);
            _root.sizeDelta = new Vector2(width + _padding * 2f, preferred.y + _padding * 2f);

            _root.SetAsLastSibling();   // draw above the panels it describes
            _visible = true;
            _group.alpha = 1f;
            Reposition();
        }

        public void Hide()
        {
            _visible = false;
            if (_group != null) _group.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (_visible) Reposition();
        }

        private void Reposition()
        {
            if (Mouse.current == null || _parentRect == null) return;

            Vector2 mouse = Mouse.current.position.ReadValue();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

            // Measure the box in screen pixels via its world corners.
            _root.GetWorldCorners(_corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
            Vector2 size = topRight - bottomLeft;

            // Hang down-right of the cursor; flip to the other side of it
            // if that would run off the screen, then clamp as a last resort.
            Vector2 topLeft = mouse + _cursorOffset;
            if (topLeft.x + size.x > Screen.width) topLeft.x = mouse.x - _cursorOffset.x - size.x;
            if (topLeft.y - size.y < 0f) topLeft.y = mouse.y - _cursorOffset.y + size.y;
            topLeft.x = Mathf.Clamp(topLeft.x, 0f, Mathf.Max(0f, Screen.width - size.x));
            topLeft.y = Mathf.Clamp(topLeft.y, Mathf.Min(size.y, Screen.height), Screen.height);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_parentRect, topLeft, cam, out var world))
                _root.position = world;
        }
    }
}
