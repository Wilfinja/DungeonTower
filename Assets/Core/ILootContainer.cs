using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// Something in the world holding takeable loot — a corpse's dropped
    /// gear (LootDrop) or a placed chest's authored contents (Chest, in
    /// DungeonTower.Items). The loot window reads this to build its rows
    /// and calls the matching Take method when the player clicks one;
    /// taking removes the item from the container for good.
    /// </summary>
    public interface ILootContainer
    {
        GridPosition Position { get; }

        // How close the player has to be to loot this container — 0
        // means "must be standing on the same tile" (a corpse pile), 1
        // means "on it or adjacent" (a chest). Kept per-container rather
        // than hardcoded per-type so a future container kind can define
        // its own reach.
        int InteractionRadius { get; }

        bool IsEmpty { get; }

        IReadOnlyList<IWeapon> Weapons { get; }
        IReadOnlyList<IArmor> Armors { get; }
        IReadOnlyDictionary<IPotion, int> Potions { get; }
        IReadOnlyDictionary<IScroll, int> Scrolls { get; }

        void TakeWeapon(IWeapon weapon);
        void TakeArmor(IArmor armor);
        void TakePotion(IPotion potion);
        void TakeScroll(IScroll scroll);
    }
}
