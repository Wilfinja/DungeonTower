using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DungeonTower.UI
{
    /// <summary>
    /// One status icon: a colored ring (green = beneficial, red = harmful),
    /// an inner circle/sprite, and an optional count in the corner.
    /// Pure view — StatusRowView tells it what to show.
    ///
    /// Prefab layout:
    ///   StatusIcon (Image = _border, this script)
    ///     Icon     (Image = _icon)
    ///     Count    (TextMeshProUGUI = _count)
    /// </summary>
    public sealed class StatusIconView : MonoBehaviour
    {
        [SerializeField] private Image _border;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _count;

        private Sprite _placeholderSprite;

        private void Awake()
        {
            // Remember the prefab's circle so an icon that briefly showed a
            // real sprite can go back to the placeholder when reused.
            _placeholderSprite = _icon.sprite;

            // These icons float over the battlefield — they must never eat
            // clicks meant for the grid.
            _border.raycastTarget = false;
            _icon.raycastTarget = false;
            if (_count != null) _count.raycastTarget = false;
        }

        /// <param name="count">0 (or less) hides the number.</param>
        public void Bind(StatusVisualsSO.Entry visual, Color borderColor, int count)
        {
            _border.color = borderColor;

            if (visual.Sprite != null)
            {
                // Real art: show it as authored, no tint.
                _icon.sprite = visual.Sprite;
                _icon.color = Color.white;
            }
            else
            {
                // Placeholder: the prefab's circle, tinted.
                _icon.sprite = _placeholderSprite;
                _icon.color = visual.Color;
            }

            if (_count != null)
            {
                bool show = count > 0;
                _count.gameObject.SetActive(show);
                if (show) _count.text = count.ToString();
            }
        }
    }
}
