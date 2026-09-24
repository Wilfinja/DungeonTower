namespace DungeonTower.Core
{
    /// <summary>
    /// The second and last equip slot: a stat-gated passive that boosts
    /// derived stats (HP, defenses, etc.) — never Body/Mind/Spirit
    /// directly, since those only grow from leveling. Implemented by
    /// ArmorSO (DungeonTower.Items) — Core never references
    /// ScriptableObject directly, so this stays engine-free.
    /// </summary>
    public interface IArmor
    {
        string Name { get; }

        // The armor's category (Plate, Robe, Cloak, ...) — same idea as
        // IWeapon.Group.
        ArmorId Group { get; }
        PrimaryStat RequiredStat { get; }
        int RequiredStatValue { get; }
        DerivedStatBonus PassiveBonus { get; }

        // False for something intrinsic to the creature (thick hide,
        // etc.) rather than a real piece of gear, so it never shows up
        // as loot when that unit dies.
        bool DropsOnDeath { get; }

        bool CanEquip(StatBlock stats);
    }
}
