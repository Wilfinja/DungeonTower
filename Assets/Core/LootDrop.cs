using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Loot sitting on a tile after a unit died there, waiting to be
    /// picked up. A dead unit drops what it had equipped (a dead player's
    /// gear becomes loot the same way, so allies can reclaim it if they
    /// can reach the tile), and a dead enemy may also drop rolled
    /// potions/scrolls from its EnemySO drop table (see DropTable).
    /// Implements ILootContainer alongside Chest, which the loot window
    /// doesn't need to tell apart from a corpse pile.
    /// </summary>
    public sealed class LootDrop : ILootContainer
    {
        private readonly List<IWeapon> _weapons = new List<IWeapon>();
        private readonly List<IArmor> _armor = new List<IArmor>();
        private readonly Dictionary<IPotion, int> _potions = new Dictionary<IPotion, int>();
        private readonly Dictionary<IScroll, int> _scrolls = new Dictionary<IScroll, int>();

        public GridPosition Position { get; }

        // Must be standing directly on the pile — unlike a chest, which
        // can be looted from an adjacent tile too.
        public int InteractionRadius => 0;

        public LootDrop(GridPosition position, IWeapon weapon, IArmor armor)
        {
            Position = position;
            if (weapon != null) _weapons.Add(weapon);
            if (armor != null) _armor.Add(armor);
        }

        public bool IsEmpty => _weapons.Count == 0 && _armor.Count == 0
            && _potions.Count == 0 && _scrolls.Count == 0;

        public IReadOnlyList<IWeapon> Weapons => _weapons;
        public IReadOnlyList<IArmor> Armors => _armor;
        public IReadOnlyDictionary<IPotion, int> Potions => _potions;
        public IReadOnlyDictionary<IScroll, int> Scrolls => _scrolls;

        public void AddPotion(IPotion potion, int count = 1)
        {
            if (potion == null || count <= 0) return;
            _potions.TryGetValue(potion, out var existing);
            _potions[potion] = existing + count;
        }

        public void AddScroll(IScroll scroll, int count = 1)
        {
            if (scroll == null || count <= 0) return;
            _scrolls.TryGetValue(scroll, out var existing);
            _scrolls[scroll] = existing + count;
        }

        public void TakeWeapon(IWeapon weapon) => _weapons.Remove(weapon);
        public void TakeArmor(IArmor armor) => _armor.Remove(armor);

        // Takes ONE copy, matching how the loot window and Take All
        // call these (Take All loops once per copy).
        public void TakePotion(IPotion potion)
        {
            if (potion == null || !_potions.TryGetValue(potion, out var count)) return;
            if (count <= 1) _potions.Remove(potion);
            else _potions[potion] = count - 1;
        }

        public void TakeScroll(IScroll scroll)
        {
            if (scroll == null || !_scrolls.TryGetValue(scroll, out var count)) return;
            if (count <= 1) _scrolls.Remove(scroll);
            else _scrolls[scroll] = count - 1;
        }
    }
}
