using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// A unit's limited consumable loadout — a fixed number of slots,
    /// each holding one stack of one potion or scroll (or empty). This
    /// is what a unit can actually reach for mid-battle; the shared
    /// PartyInventory is the party's overall stash, and (once that UI
    /// exists) belts get loaded from it between battles rather than
    /// letting a unit draw on the whole shared pool directly — that's
    /// the actual point of having a belt at all.
    /// </summary>
    public sealed class Belt
    {
        private readonly IConsumable[] _items;
        private readonly int[] _counts;

        public int SlotCount => _items.Length;

        public Belt(int slotCount)
        {
            _items = new IConsumable[slotCount];
            _counts = new int[slotCount];
        }

        public IConsumable GetItem(int slot) => InRange(slot) ? _items[slot] : null;

        public int GetCount(int slot) => InRange(slot) ? _counts[slot] : 0;

        public bool IsSlotEmpty(int slot) => GetItem(slot) == null || GetCount(slot) <= 0;

        // Loads an item+count into a slot, replacing whatever was there.
        // The caller is responsible for returning any bumped item back
        // to the shared PartyInventory first — this doesn't know about
        // PartyInventory at all, on purpose, so Belt has no dependency
        // on it.
        public void SetSlot(int slot, IConsumable item, int count)
        {
            if (!InRange(slot)) return;
            bool hasItem = item != null && count > 0;
            _items[slot] = hasItem ? item : null;
            _counts[slot] = hasItem ? count : 0;
        }

        public void ClearSlot(int slot) => SetSlot(slot, null, 0);

        // Consumes one copy of this exact item from whichever slot holds
        // it. Returns false (no change) if it isn't in the belt at all.
        public bool TryConsume(IConsumable item)
        {
            if (item == null) return false;

            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i] != item || _counts[i] <= 0) continue;
                _counts[i]--;
                if (_counts[i] <= 0) _items[i] = null;
                return true;
            }
            return false;
        }

        private bool InRange(int slot) => slot >= 0 && slot < _items.Length;
    }
}
