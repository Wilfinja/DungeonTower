using System.Collections.Generic;
using UnityEngine;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Row showing the upcoming turn order: the rest of this round
    /// (current unit first), then the next round, dimmed, with an
    /// optional divider between rounds. Purely a view — who's worth
    /// showing is decided by whoever builds the forecast (see
    /// Battle.GetProjectedTurnOrder), and BattleController pushes it in
    /// via Refresh. Hides entirely outside of combat.
    /// </summary>
    public sealed class TurnTrackerView : MonoBehaviour
    {
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private TurnTrackerSlotView _slotPrefab;
        [SerializeField] private GameObject _roundDividerPrefab; // optional
        [SerializeField, Min(1)] private int _maxSlots = 12;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        // inCombat is passed in (rather than derived from `order`)
        // because an alerted enemy that already acted this round isn't
        // in the remaining order, but combat is still on.
        public void Refresh(IEnumerable<TurnSlot> order, CombatUnit current, bool inCombat)
        {
            if (!inCombat)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            Clear();

            int shown = 0;
            int lastRound = -1;
            foreach (var entry in order)
            {
                if (shown >= _maxSlots) break;

                if (lastRound >= 0 && entry.RoundOffset != lastRound && _roundDividerPrefab != null)
                    _spawned.Add(Instantiate(_roundDividerPrefab, _slotContainer));
                lastRound = entry.RoundOffset;

                // The same unit can appear more than once now (this
                // round and next), so "current" means offset 0 only.
                bool isCurrent = entry.RoundOffset == 0 && entry.Unit == current;

                var slot = Instantiate(_slotPrefab, _slotContainer);
                slot.Bind(entry.Unit, isCurrent, entry.RoundOffset);
                _spawned.Add(slot.gameObject);
                shown++;
            }
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        private void Clear()
        {
            foreach (var obj in _spawned)
                if (obj != null) Destroy(obj);
            _spawned.Clear();
        }
    }
}
