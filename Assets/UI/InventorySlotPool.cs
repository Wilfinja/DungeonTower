using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// A set of reusable InventorySlotViews under one container. Call
    /// Begin(), then Next() once per slot in display order (configuring
    /// each), then End() to hide any leftovers from a previous, longer
    /// layout. Slots are reconfigured in place rather than destroyed and
    /// rebuilt, so refreshing doesn't disturb scroll position or an
    /// in-flight drag. Slot N is always the Nth child of the container.
    /// </summary>
    public sealed class InventorySlotPool
    {
        private readonly Transform _container;
        private readonly InventorySlotView _prefab;
        private readonly Action<InventorySlotView> _onCreated;
        private readonly List<InventorySlotView> _slots = new List<InventorySlotView>();
        private int _next;

        // onCreated runs once per new slot — the place to subscribe to
        // its drag/drop events.
        public InventorySlotPool(Transform container, InventorySlotView prefab, Action<InventorySlotView> onCreated)
        {
            _container = container;
            _prefab = prefab;
            _onCreated = onCreated;
        }

        public void Begin() => _next = 0;

        public InventorySlotView Next()
        {
            InventorySlotView slot;
            if (_next < _slots.Count)
            {
                slot = _slots[_next];
            }
            else
            {
                slot = UnityEngine.Object.Instantiate(_prefab, _container);
                _slots.Add(slot);
                _onCreated?.Invoke(slot);
            }

            slot.gameObject.SetActive(true);
            _next++;
            return slot;
        }

        public void End()
        {
            for (int i = _next; i < _slots.Count; i++)
                _slots[i].gameObject.SetActive(false);
        }

        // Every slot currently showing in this pool's layout — what a
        // drag-highlight pass should consider. Safe to call at any time,
        // including mid-drag.
        public IEnumerable<InventorySlotView> Active()
        {
            foreach (var slot in _slots)
                if (slot.gameObject.activeSelf) yield return slot;
        }
    }
}
