using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// One unit's entry in the turn tracker: its glyph (same first-letter
    /// convention as UnitView) tinted by faction, with the current unit
    /// shown bold, slightly larger, and on a lighter background.
    /// </summary>
    public sealed class TurnTrackerSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _glyph;
        [SerializeField] private Image _background; // optional
        [SerializeField] private Color _playerColor = Color.cyan;
        [SerializeField] private Color _enemyColor = Color.red;
        [SerializeField] private Color _currentBackground = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color _idleBackground = new Color(0f, 0f, 0f, 0.35f);

        public void Bind(CombatUnit unit, bool isCurrent)
        {
            _glyph.text = unit.DisplayName[0].ToString();
            _glyph.color = unit.Faction == Faction.Player ? _playerColor : _enemyColor;
            _glyph.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
            if (_background != null)
                _background.color = isCurrent ? _currentBackground : _idleBackground;
            transform.localScale = isCurrent ? Vector3.one * 1.2f : Vector3.one;
        }
    }
}
