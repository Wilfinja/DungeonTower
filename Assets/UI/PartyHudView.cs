using System;
using System.Collections.Generic;
using UnityEngine;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// The party's HP and MP, always on screen: one PartyHudRowView per
    /// hero, in party order, with the selected hero highlighted. Click a
    /// row to select that hero (BattleController decides whether that's
    /// allowed right now). Reads the units every frame, so it never needs
    /// telling about damage, healing or MP spent.
    /// </summary>
    public sealed class PartyHudView : MonoBehaviour
    {
        [Tooltip("The object the rows are created under (give it a Vertical Layout Group, or lay the rows out by hand).")]
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private PartyHudRowView _rowPrefab;

        private readonly List<(CombatUnit unit, PartyHudRowView row)> _rows = new List<(CombatUnit, PartyHudRowView)>();
        private Func<CombatUnit> _selected;
        private Func<bool> _inCombat;

        public event Action<CombatUnit> HeroClicked;

        /// <param name="selected">Returns the hero currently acting (or null).</param>
        /// <param name="inCombat">Returns true while a fight is on (shows actions left).</param>
        public void Bind(IReadOnlyList<CombatUnit> party, Func<CombatUnit> selected, Func<bool> inCombat)
        {
            if (_rowContainer == null || _rowPrefab == null)
            {
                Debug.LogError("PartyHudView: assign the Row Container and Row Prefab in the Inspector.", this);
                return;
            }

            foreach (var entry in _rows)
                if (entry.row != null) Destroy(entry.row.gameObject);
            _rows.Clear();

            _selected = selected;
            _inCombat = inCombat;

            foreach (var hero in party)
            {
                var row = Instantiate(_rowPrefab, _rowContainer);
                var captured = hero;
                if (row.Button != null) row.Button.onClick.AddListener(() => HeroClicked?.Invoke(captured));
                _rows.Add((hero, row));
            }
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var current = _selected != null ? _selected() : null;
            bool combat = _inCombat != null && _inCombat();
            foreach (var entry in _rows)
                entry.row.Refresh(entry.unit, entry.unit == current, combat);
        }
    }
}
