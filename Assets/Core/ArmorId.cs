namespace DungeonTower.Core
{
    /// <summary>
    /// Stable identifier for an armor asset, set on the ArmorSO in the
    /// Inspector. Code asks ItemRegistry.GetArmor(ArmorId) instead of
    /// holding a direct asset reference — add new entries here whenever a
    /// new armor asset needs to be looked up by code.
    /// </summary>
    public enum ArmorId
    {
        Plate,
        Robe,
        Cloak
    }
}
