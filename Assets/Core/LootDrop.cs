namespace DungeonTower.Core
{
    /// <summary>
    /// Gear sitting on a tile after a unit died there, waiting to be
    /// picked up. A dead enemy drops what it had equipped; a dead player's
    /// gear becomes loot the same way, so allies can reclaim it if they
    /// can reach the tile.
    /// </summary>
    public sealed class LootDrop
    {
        public GridPosition Position { get; }
        public IWeapon Weapon { get; }
        public IArmor Armor { get; }

        public LootDrop(GridPosition position, IWeapon weapon, IArmor armor)
        {
            Position = position;
            Weapon = weapon;
            Armor = armor;
        }
    }
}
