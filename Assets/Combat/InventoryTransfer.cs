using System;
using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    public enum SlotZone { Stash, Weapon, Armor, Belt }

    /// <summary>
    /// Addresses one slot anywhere in the inventory UI: a stash cell, a
    /// party member's weapon or armor slot, or one of their belt slots.
    /// Owner is the member's party index (-1 for the stash); Index is the
    /// stash or belt slot number (0 for equipment).
    /// </summary>
    public readonly struct SlotRef : IEquatable<SlotRef>
    {
        public SlotZone Zone { get; }
        public int Owner { get; }
        public int Index { get; }

        private SlotRef(SlotZone zone, int owner, int index)
        {
            Zone = zone;
            Owner = owner;
            Index = index;
        }

        public static SlotRef Stash(int index) => new SlotRef(SlotZone.Stash, -1, index);
        public static SlotRef WeaponOf(int owner) => new SlotRef(SlotZone.Weapon, owner, 0);
        public static SlotRef ArmorOf(int owner) => new SlotRef(SlotZone.Armor, owner, 0);
        public static SlotRef BeltOf(int owner, int index) => new SlotRef(SlotZone.Belt, owner, index);

        // True for anything that's part of a unit's own loadout.
        public bool BelongsToUnit => Zone != SlotZone.Stash;

        public bool Equals(SlotRef other) => Zone == other.Zone && Owner == other.Owner && Index == other.Index;
        public override bool Equals(object obj) => obj is SlotRef other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Zone * 397) ^ Owner) * 397 ^ Index);
    }

    public enum MoveOutcome
    {
        // Nothing changed (invalid move, or dropped on itself).
        Rejected,
        // Changed, but only stash arrangement or one belt's order — no
        // unit's loadout actually differs, so it should never cost a turn.
        Rearranged,
        // A unit's equipment or belt contents changed.
        LoadoutChanged
    }

    /// <summary>
    /// The one place that knows how dragging an item from any slot onto
    /// any other slot behaves. A move is a stack merge (same potion/
    /// scroll), a split (part of a stack peeled onto an empty slot), or
    /// a full swap of the two slots' contents — and it only happens if
    /// both slots can legally hold what they'd receive (stat
    /// requirements for gear, consumables only on belts). A partial
    /// amount can only land on an empty slot or merge into a matching
    /// stack; peeling part of a stack onto something else that's
    /// occupied is rejected rather than guessing what to do with the
    /// leftover. Rules about WHEN a move is allowed or what it costs
    /// stay with the caller. Slots belonging to a fallen unit are off
    /// limits.
    /// </summary>
    public static class InventoryTransfer
    {
        // amount caps how many copies move — pass int.MaxValue for "the
        // whole stack" (clamped automatically to what's actually there).
        // Only meaningful for a stackable item; gear always moves as 1.
        public static MoveOutcome TryMove(PartyInventory stash, IReadOnlyList<CombatUnit> party,
            SlotRef from, SlotRef to, int amount, out string reason)
        {
            reason = null;
            if (from.Equals(to)) return MoveOutcome.Rejected;

            if (!TryRead(stash, party, from, out var source) || !TryRead(stash, party, to, out var target))
            {
                reason = "That slot isn't available.";
                return MoveOutcome.Rejected;
            }
            if (source.IsEmpty) return MoveOutcome.Rejected;

            int moving = Math.Min(Math.Max(amount, 1), source.Count);

            bool sameItemStack = !target.IsEmpty
                && ReferenceEquals(source.Item, target.Item)
                && source.Item is IConsumable
                && CanStack(from) && CanStack(to);

            if (sameItemStack)
            {
                Write(stash, party, to, new ItemStack(target.Item, target.Count + moving));
                int remaining = source.Count - moving;
                Write(stash, party, from, remaining > 0 ? new ItemStack(source.Item, remaining) : default);
            }
            else if (moving < source.Count)
            {
                // Splitting a stack only makes sense onto an empty slot —
                // there's no sensible thing to do with the leftover if
                // the target is occupied by something else.
                if (!target.IsEmpty)
                {
                    reason = "Can't split part of a stack onto an occupied slot.";
                    return MoveOutcome.Rejected;
                }
                var moved = new ItemStack(source.Item, moving);
                if (!Accepts(party, to, moved))
                {
                    reason = RejectReason(party, to, moved);
                    return MoveOutcome.Rejected;
                }
                Write(stash, party, to, moved);
                Write(stash, party, from, new ItemStack(source.Item, source.Count - moving));
            }
            else
            {
                if (!Accepts(party, to, source))
                {
                    reason = RejectReason(party, to, source);
                    return MoveOutcome.Rejected;
                }
                if (!Accepts(party, from, target))
                {
                    reason = RejectReason(party, from, target);
                    return MoveOutcome.Rejected;
                }
                Write(stash, party, to, source);
                Write(stash, party, from, target);
            }

            bool sameBelt = from.Zone == SlotZone.Belt && to.Zone == SlotZone.Belt && from.Owner == to.Owner;
            bool touchesUnit = from.BelongsToUnit || to.BelongsToUnit;
            return touchesUnit && !sameBelt ? MoveOutcome.LoadoutChanged : MoveOutcome.Rearranged;
        }

        // Read-only check for whether dropping whatever's in `from` onto
        // `to` would do anything (a merge or a full swap) — used to
        // highlight valid drop targets while a drag is in flight. Doesn't
        // account for a partial (shift-drag) amount, since that only
        // ever relaxes what's allowed (a split can land anywhere an
        // empty slot could already accept a full stack).
        public static bool CanAccept(PartyInventory stash, IReadOnlyList<CombatUnit> party, SlotRef from, SlotRef to)
        {
            if (from.Equals(to)) return false;
            if (!TryRead(stash, party, from, out var source) || source.IsEmpty) return false;
            if (!TryRead(stash, party, to, out var target)) return false;

            bool sameItemStack = !target.IsEmpty
                && ReferenceEquals(source.Item, target.Item)
                && source.Item is IConsumable
                && CanStack(from) && CanStack(to);
            if (sameItemStack) return true;

            if (target.IsEmpty) return Accepts(party, to, source);

            return Accepts(party, to, source) && Accepts(party, from, target);
        }

        public static string NameOf(object item)
        {
            switch (item)
            {
                case IWeapon weapon: return weapon.Name;
                case IArmor armor: return armor.Name;
                case IConsumable consumable: return consumable.Name;
                default: return "Item";
            }
        }

        private static bool CanStack(SlotRef slot) => slot.Zone == SlotZone.Stash || slot.Zone == SlotZone.Belt;

        private static bool TryRead(PartyInventory stash, IReadOnlyList<CombatUnit> party, SlotRef slot, out ItemStack content)
        {
            content = default;

            if (slot.Zone == SlotZone.Stash)
            {
                if (slot.Index < 0) return false;
                content = stash.GetSlot(slot.Index);
                return true;
            }

            if (slot.Owner < 0 || slot.Owner >= party.Count) return false;
            var unit = party[slot.Owner];
            if (!unit.IsAlive) return false;

            switch (slot.Zone)
            {
                case SlotZone.Weapon:
                {
                    var weapon = unit.EquippedWeapon;
                    content = weapon != null ? new ItemStack(weapon, 1) : default;
                    return true;
                }
                case SlotZone.Armor:
                {
                    var armor = unit.Stats.EquippedArmor;
                    content = armor != null ? new ItemStack(armor, 1) : default;
                    return true;
                }
                case SlotZone.Belt:
                {
                    if (slot.Index < 0 || slot.Index >= unit.Belt.SlotCount) return false;
                    content = unit.Belt.IsSlotEmpty(slot.Index)
                        ? default
                        : new ItemStack(unit.Belt.GetItem(slot.Index), unit.Belt.GetCount(slot.Index));
                    return true;
                }
                default:
                    return false;
            }
        }

        private static void Write(PartyInventory stash, IReadOnlyList<CombatUnit> party, SlotRef slot, ItemStack content)
        {
            switch (slot.Zone)
            {
                case SlotZone.Stash:
                    stash.SetSlot(slot.Index, content.Item, content.Count);
                    break;

                case SlotZone.Weapon:
                {
                    var unit = party[slot.Owner];
                    if (content.IsEmpty) unit.UnequipWeapon();
                    else unit.TryEquip((IWeapon)content.Item);
                    break;
                }

                case SlotZone.Armor:
                {
                    var unit = party[slot.Owner];
                    if (content.IsEmpty) unit.Stats.UnequipArmor();
                    else unit.Stats.TryEquipArmor((IArmor)content.Item);
                    unit.ClampVitals(); // max HP/MP can drop with the armor
                    break;
                }

                case SlotZone.Belt:
                    party[slot.Owner].Belt.SetSlot(slot.Index, (IConsumable)content.Item, content.Count);
                    break;
            }
        }

        // Anything can become empty; otherwise the slot has to be able to
        // hold that kind of item (and, for gear, the owner has to meet
        // its stat requirement).
        private static bool Accepts(IReadOnlyList<CombatUnit> party, SlotRef slot, ItemStack content)
        {
            if (content.IsEmpty) return true;

            switch (slot.Zone)
            {
                case SlotZone.Stash:
                    return true;
                case SlotZone.Weapon:
                    return content.Item is IWeapon weapon && weapon.CanEquip(party[slot.Owner].Stats.Current);
                case SlotZone.Armor:
                    return content.Item is IArmor armor && armor.CanEquip(party[slot.Owner].Stats.Current);
                case SlotZone.Belt:
                    return content.Item is IConsumable;
                default:
                    return false;
            }
        }

        private static string RejectReason(IReadOnlyList<CombatUnit> party, SlotRef slot, ItemStack content)
        {
            string name = NameOf(content.Item);
            string owner = slot.Owner >= 0 && slot.Owner < party.Count ? party[slot.Owner].DisplayName : "";

            switch (slot.Zone)
            {
                case SlotZone.Weapon:
                    return content.Item is IWeapon
                        ? $"{owner} cannot equip {name} — stat requirement not met"
                        : $"{name} isn't a weapon.";
                case SlotZone.Armor:
                    return content.Item is IArmor
                        ? $"{owner} cannot equip {name} — stat requirement not met"
                        : $"{name} isn't armor.";
                case SlotZone.Belt:
                    return $"{name} can't go on a belt.";
                default:
                    return "That move isn't allowed.";
            }
        }
    }
}
