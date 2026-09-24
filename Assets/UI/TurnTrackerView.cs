using System.Collections.Generic;
using UnityEngine;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Row at the top of the screen showing this round's remaining turn
    /// order, current unit first. Shows living party members and
    /// alerted enemies only, and hides entirely outside of combat.
    /// Purely a view — BattleController pushes state in via Refresh.
    /// </summary>
    public sealed class TurnTrackerView : MonoBehaviour
    {
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private TurnTrackerSlotView _slotPrefab;

        private readonly List<TurnTrackerSlotView> _slots = new List<TurnTrackerSlotView>();

        // inCombat is passed in (rather than derived from `order`)
        // because an alerted enemy that already acted this round isn't
        // in the remaining order, but combat is still on.
        public void Refresh(IEnumerable<CombatUnit> order, CombatUnit current, bool inCombat)
        {
            if (!inCombat)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            Clear();

            foreach (var unit in order)
            {
                if (!unit.IsAlive) continue;
                if (unit.Faction == Faction.Enemy && !unit.IsAlerted) continue;

                var slot = Instantiate(_slotPrefab, _slotContainer);
                slot.Bind(unit, unit == current);
                _slots.Add(slot);
            }
        }

        public void Hide()
        {
            Clear();
            gameObject.SetActive(false);
        }

        private void Clear()
        {
            foreach (var slot in _slots)
                if (slot != null) Destroy(slot.gameObject);
            _slots.Clear();
        }
    }
}
