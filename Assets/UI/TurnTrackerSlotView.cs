using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// One unit's entry in the turn tracker: its glyph (same first-letter
    /// convention as UnitView) tinted by faction, with the current unit
    /// shown bold, slightly larger, and on a lighter background. Entries
    /// from upcoming rounds are drawn dimmed so "acting this round" and
    /// "projected next round" read differently at a glance.
    /// </summary>
    public sealed class TurnTrackerSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _glyph;
        [SerializeField] private Image _background; // optional
        [SerializeField] private Color _playerColor = Color.cyan;
        [SerializeField] private Color _enemyColor = Color.red;
        [SerializeField] private Color _currentBackground = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color _idleBackground = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float _futureRoundAlpha = 0.55f;

        public void Bind(CombatUnit unit, bool isCurrent, int roundOffset = 0)
        {
            float alpha = roundOffset > 0 ? _futureRoundAlpha : 1f;

            var glyphColor = unit.Faction == Faction.Player ? _playerColor : _enemyColor;
            glyphColor.a *= alpha;

            _glyph.text = unit.DisplayName[0].ToString();
            _glyph.color = glyphColor;
            _glyph.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;

            if (_background != null)
            {
                var background = isCurrent ? _currentBackground : _idleBackground;
                background.a *= alpha;
                _background.color = background;
            }

            transform.localScale = isCurrent ? Vector3.one * 1.2f : Vector3.one;
        }
    }
}
