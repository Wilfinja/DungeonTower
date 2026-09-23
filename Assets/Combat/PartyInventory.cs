using System.Collections.Generic;
using DungeonTower.Core;

namespace DungeonTower.Combat
{
    /// <summary>
    /// The party's shared pool of unequipped gear and consumables.
    /// Weapons/armor are unique instances — equipping moves one in or
    /// out of these lists (see BattleController's equip handlers).
    /// Potions/scrolls are stack-counted instead, since carrying several
    /// of the same one is the normal case. Works entirely in terms of
    /// Core's interfaces (IWeapon/IArmor/IPotion/IScroll) rather than the
    /// concrete Items ScriptableObjects, so Combat never needs a
    /// reference to the Items assembly.
    /// </summary>
    public sealed class PartyInventory
    {
        private readonly List<IWeapon> _weapons = new List<IWeapon>();
        private readonly List<IArmor> _armor = new List<IArmor>();
        private readonly Dictionary<IPotion, int> _potions = new Dictionary<IPotion, int>();
        private readonly Dictionary<IScroll, int> _scrolls = new Dictionary<IScroll, int>();

        public IReadOnlyList<IWeapon> Weapons => _weapons;
        public IReadOnlyList<IArmor> Armors => _armor;
        public IReadOnlyDictionary<IPotion, int> Potions => _potions;
        public IReadOnlyDictionary<IScroll, int> Scrolls => _scrolls;

        public void AddWeapon(IWeapon weapon)
        {
            if (weapon != null) _weapons.Add(weapon);
        }

        public void RemoveWeapon(IWeapon weapon)
        {
            if (weapon != null) _weapons.Remove(weapon);
        }

        public void AddArmor(IArmor armor)
        {
            if (armor != null) _armor.Add(armor);
        }

        public void RemoveArmor(IArmor armor)
        {
            if (armor != null) _armor.Remove(armor);
        }

        public void AddPotion(IPotion potion, int count = 1)
        {
            if (potion == null || count <= 0) return;
            _potions.TryGetValue(potion, out var existing);
            _potions[potion] = existing + count;
        }

        // Returns false (and changes nothing) if none are left to consume.
        public bool TryConsumePotion(IPotion potion)
        {
            if (potion == null || !_potions.TryGetValue(potion, out var count) || count <= 0) return false;
            if (count == 1) _potions.Remove(potion);
            else _potions[potion] = count - 1;
            return true;
        }

        public void AddScroll(IScroll scroll, int count = 1)
        {
            if (scroll == null || count <= 0) return;
            _scrolls.TryGetValue(scroll, out var existing);
            _scrolls[scroll] = existing + count;
        }

        public bool TryConsumeScroll(IScroll scroll)
        {
            if (scroll == null || !_scrolls.TryGetValue(scroll, out var count) || count <= 0) return false;
            if (count == 1) _scrolls.Remove(scroll);
            else _scrolls[scroll] = count - 1;
            return true;
        }
    }
}
