namespace DungeonTower.Core
{
    /// <summary>
    /// Stable identifier for a weapon asset, set on the WeaponSO in the
    /// Inspector. Code asks ItemRegistry.GetWeapon(WeaponId) instead of
    /// holding a direct asset reference — add new entries here whenever a
    /// new weapon asset needs to be looked up by code (starting kits,
    /// loot tables, etc.).
    /// </summary>
    public enum WeaponId
    {
        Sword,
        Mace,
        SwordandShield,
        AxeandShield,
        Staff,
        Spear,
        WarAxe,
        Bow
    }
}
