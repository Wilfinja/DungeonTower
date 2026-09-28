using UnityEngine;
using TMPro;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Visual for one CombatUnit — a colored glyph via TextMeshPro, matching
    /// the Nethack-style plan. Swap for sprites later without touching
    /// BattleController. Call Refresh() any time the unit's position or
    /// alive-state changes.
    ///
    /// Visible on screen requires BOTH alive and fog-visible: the party's
    /// own units are always fog-visible (BattleController never hides
    /// them), while an enemy is only shown while standing on a currently
    /// lit tile. Defaults to fog-visible so any scene that never calls
    /// SetFogVisible behaves exactly as before fog existed.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _glyph;
        [SerializeField] private Color _playerColor = Color.cyan;
        [SerializeField] private Color _enemyColor = Color.red;

        private CombatUnit _unit;
        private bool _fogVisible = true;

        public void Bind(CombatUnit unit, char symbol)
        {
            _unit = unit;
            _glyph.text = symbol.ToString();
            _glyph.color = unit.Faction == Faction.Player ? _playerColor : _enemyColor;
            Refresh();
        }

        public void Refresh()
        {
            if (_unit == null) return;
            transform.position = GridToWorld.ToWorldPosition(_unit.Position);
            ApplyVisibility();
        }

        public void SetFogVisible(bool visible)
        {
            _fogVisible = visible;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (_unit == null) return;
            gameObject.SetActive(_unit.IsAlive && _fogVisible);
        }

        // Visual cue for whose turn it is — bigger and bold while active,
        // normal otherwise. Swap for a proper indicator (arrow, ring) later.
        public void SetHighlighted(bool isCurrentTurn)
        {
            if (_glyph != null)
                _glyph.fontStyle = isCurrentTurn ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            transform.localScale = isCurrentTurn ? Vector3.one * 1.3f : Vector3.one;
        }
    }
}
