using DungeonTower.Combat;
using DungeonTower.Core;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
        [SerializeField] private float _tilesPerSecond = 8f;

        [Header("Attack animation")]
        [SerializeField, Min(0f)] private float _pullBackTiles = 0.25f;
        [SerializeField, Min(0f)] private float _lungeTiles = 0.35f;
        [SerializeField, Min(0.01f)] private float _snapSeconds = 0.06f;
        [SerializeField, Min(0.01f)] private float _returnSeconds = 0.12f;

        private CombatUnit _unit;
        private bool _attacking;
        private bool _fogVisible = true;
        private readonly Queue<Vector3> _waypoints = new Queue<Vector3>();

        public bool IsMoving => _waypoints.Count > 0;

        // Slides toward one tile. No-op while hidden in fog; the arrival
        // Refresh() places it instead.
        public void StepTo(GridPosition tile)
        {
            if (!gameObject.activeInHierarchy) return;
            _waypoints.Enqueue(GridToWorld.ToWorldPosition(tile));
        }

        private void Update()
        {
            if (_waypoints.Count == 0) return;
            float tileSize = TileWorldSize();
            var target = _waypoints.Peek();
            transform.position = Vector3.MoveTowards(
                transform.position, target, tileSize * _tilesPerSecond * Time.deltaTime);
            if (transform.position == target) _waypoints.Dequeue();
        }

        public static float TileWorldSize() => Vector3.Distance(
            GridToWorld.ToWorldPosition(new GridPosition(0, 0)),
            GridToWorld.ToWorldPosition(new GridPosition(1, 0)));

        // Slow pull-back, fast snap toward the target, then settle home.
        // onSnap fires the instant the glyph snaps forward, so a melee
        // effect can appear with the thrust. No direction (a self-targeted
        // ability) skips everything; a glyph hidden in fog skips the
        // motion but still fires onSnap so the effect can play.
        public IEnumerator PlayAttack(Vector3 worldDirection, float windUpSeconds, System.Action onSnap = null)
        {
            if (worldDirection.sqrMagnitude < 0.0001f) yield break;
            if (!gameObject.activeInHierarchy) { onSnap?.Invoke(); yield break; }

            _attacking = true;
            var home = transform.position;
            var dir = worldDirection.normalized;
            float tile = TileWorldSize();

            yield return Slide(home, home - dir * tile * _pullBackTiles, windUpSeconds, true);            // pull back
            onSnap?.Invoke();
            yield return Slide(transform.position, home + dir * tile * _lungeTiles, _snapSeconds, false); // snap
            yield return Slide(transform.position, home, _returnSeconds, true);                           // settle

            transform.position = home;
            _attacking = false;
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float seconds, bool smooth)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                transform.position = Vector3.Lerp(from, to, smooth ? Mathf.SmoothStep(0f, 1f, k) : k);
                yield return null;
            }
            transform.position = to;
        }

        // If fog/death hides the view mid-walk, don't leave it stuck "moving".
        private void OnDisable()
        {
            _attacking = false;
            _waypoints.Clear();
            if (_unit != null) transform.position = GridToWorld.ToWorldPosition(_unit.Position);
        }

        public void Bind(CombatUnit unit, char symbol)
        {
            _unit = unit;
            _glyph.text = symbol.ToString();
            _glyph.color = unit.Faction == Faction.Player ? _playerColor : _enemyColor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnitDebugInspector.Attach(this, unit);
#endif

            Refresh();
        }

        public void Refresh()
        {
            if (_unit == null) return;
            if (!IsMoving && !_attacking) transform.position = GridToWorld.ToWorldPosition(_unit.Position);
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
