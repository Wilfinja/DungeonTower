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
        ArmorId Id { get; }
        PrimaryStat RequiredStat { get; }
        int RequiredStatValue { get; }
        DerivedStatBonus PassiveBonus { get; }

        bool CanEquip(StatBlock stats);
    }
}
