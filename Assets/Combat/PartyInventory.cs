using System.Collections.Generic;
using System.Linq;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// One occupied (or empty) cell of the stash: a single item and how
    /// many of it are stacked there. Item is an IWeapon, IArmor, IPotion
    /// or IScroll (typed as object because those interfaces share no
    /// base); gear always has a Count of 1, consumables stack.
    /// </summary>
    public readonly struct ItemStack
    {
        public object Item { get; }
        public int Count { get; }

        public ItemStack(object item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;
    }

    /// <summary>
    /// The party's shared stash of unequipped gear and consumables,
    /// stored as positional slots so the player's arrangement (via
    /// drag and drop) is remembered. Add* puts an item in the first
    /// free slot (consumables join an existing stack of the same item);
    /// Remove*/TryConsume* take one copy from wherever it sits. Works
    /// entirely in terms of Core's interfaces, so Combat never needs a
    /// reference to the Items assembly. The Weapons/Armors/Potions/
    /// Scrolls views are derived on demand and ignore arrangement.
    /// </summary>
    public sealed class PartyInventory
    {
        private readonly List<ItemStack> _slots = new List<ItemStack>();

        public int SlotCount => _slots.Count;

        public ItemStack GetSlot(int index)
            => index >= 0 && index < _slots.Count ? _slots[index] : default;

        // Writes a slot directly, growing the stash if index is past the
        // end. A null item or non-positive count clears the slot.
        public void SetSlot(int index, object item, int count)
        {
            if (index < 0) return;
            while (_slots.Count <= index) _slots.Add(default);
            _slots[index] = item != null && count > 0 ? new ItemStack(item, count) : default;
        }

        public IReadOnlyList<IWeapon> Weapons
            => _slots.Where(s => !s.IsEmpty && s.Item is IWeapon).Select(s => (IWeapon)s.Item).ToList();

        public IReadOnlyList<IArmor> Armors
            => _slots.Where(s => !s.IsEmpty && s.Item is IArmor).Select(s => (IArmor)s.Item).ToList();

        public IReadOnlyDictionary<IPotion, int> Potions => Totals<IPotion>();
        public IReadOnlyDictionary<IScroll, int> Scrolls => Totals<IScroll>();

        public void AddWeapon(IWeapon weapon)
        {
            if (weapon != null) AddToFirstEmpty(weapon, 1);
        }

        public void RemoveWeapon(IWeapon weapon)
        {
            if (weapon != null) RemoveOne(weapon);
        }

        public void AddArmor(IArmor armor)
        {
            if (armor != null) AddToFirstEmpty(armor, 1);
        }

        public void RemoveArmor(IArmor armor)
        {
            if (armor != null) RemoveOne(armor);
        }

        public void AddPotion(IPotion potion, int count = 1) => AddStackable(potion, count);

        // Returns false (and changes nothing) if none are left to consume.
        public bool TryConsumePotion(IPotion potion) => potion != null && RemoveOne(potion);

        public void AddScroll(IScroll scroll, int count = 1) => AddStackable(scroll, count);

        public bool TryConsumeScroll(IScroll scroll) => scroll != null && RemoveOne(scroll);

        private void AddToFirstEmpty(object item, int count)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                _slots[i] = new ItemStack(item, count);
                return;
            }
            _slots.Add(new ItemStack(item, count));
        }

        private void AddStackable(object item, int count)
        {
            if (item == null || count <= 0) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty || !ReferenceEquals(_slots[i].Item, item)) continue;
                _slots[i] = new ItemStack(item, _slots[i].Count + count);
                return;
            }
            AddToFirstEmpty(item, count);
        }

        // Takes one copy: decrements a stack, or clears the slot when
        // that was the last one.
        private bool RemoveOne(object item)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty || !ReferenceEquals(_slots[i].Item, item)) continue;
                int remaining = _slots[i].Count - 1;
                _slots[i] = remaining > 0 ? new ItemStack(item, remaining) : default;
                return true;
            }
            return false;
        }

        private Dictionary<T, int> Totals<T>() where T : class
        {
            var totals = new Dictionary<T, int>();
            foreach (var slot in _slots)
            {
                if (slot.IsEmpty || !(slot.Item is T item)) continue;
                totals.TryGetValue(item, out var existing);
                totals[item] = existing + slot.Count;
            }
            return totals;
        }
    }
}
