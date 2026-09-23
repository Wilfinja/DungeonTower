namespace DungeonTower.Core
{
    /// <summary>
    /// Shared identity for one-use inventory items — potions and
    /// scrolls. Unlike IWeapon/IArmor there's no equip slot to fill; this
    /// is just enough for inventory UI to list an item and check whether
    /// a unit is allowed to use it. RequiredStatValue of 0 means anyone
    /// can use it (the typical case for a potion). The actual effect —
    /// heal vs. cast — lives on the concrete PotionSO/ScrollSO, resolved
    /// by whatever consumes it (mirrors how AttackResolver consumes an
    /// IAbility rather than IWeapon knowing how to resolve itself).
    /// </summary>
    public interface IConsumable
    {
        string Name { get; }
        PrimaryStat RequiredStat { get; }
        int RequiredStatValue { get; }

        bool CanUse(StatBlock stats);
    }
}
