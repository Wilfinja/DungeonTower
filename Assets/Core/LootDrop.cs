using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Gear sitting on a tile after a unit died there, waiting to be
    /// picked up. A dead enemy drops what it had equipped; a dead
    /// player's gear becomes loot the same way, so allies can reclaim it
    /// if they can reach the tile. Corpses never drop potions/scrolls —
    /// only whatever was equipped — so those two collections are always
    /// empty; they exist so this can implement ILootContainer alongside
    /// Chest, which the loot window doesn't need to tell apart from a
    /// corpse pile.
    /// </summary>
    public sealed class LootDrop : ILootContainer
    {
        private readonly List<IWeapon> _weapons = new List<IWeapon>();
        private readonly List<IArmor> _armor = new List<IArmor>();
        private static readonly Dictionary<IPotion, int> EmptyPotions = new Dictionary<IPotion, int>();
        private static readonly Dictionary<IScroll, int> EmptyScrolls = new Dictionary<IScroll, int>();

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

        public bool IsEmpty => _weapons.Count == 0 && _armor.Count == 0;

        public IReadOnlyList<IWeapon> Weapons => _weapons;
        public IReadOnlyList<IArmor> Armors => _armor;
        public IReadOnlyDictionary<IPotion, int> Potions => EmptyPotions;
        public IReadOnlyDictionary<IScroll, int> Scrolls => EmptyScrolls;

        public void TakeWeapon(IWeapon weapon) => _weapons.Remove(weapon);
        public void TakeArmor(IArmor armor) => _armor.Remove(armor);
        public void TakePotion(IPotion potion) { } // corpses never carry these
        public void TakeScroll(IScroll scroll) { }
    }
}
