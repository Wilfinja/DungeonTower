using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// The single equippable item type carrying combat behavior: a
    /// stat-gated weapon with one or two abilities. Implemented by
    /// WeaponSO (DungeonTower.Items) — Core never references
    /// ScriptableObject directly, so this stays engine-free.
    /// </summary>
    public interface IWeapon
    {
        string Name { get; }
        WeaponId Id { get; }
        PrimaryStat RequiredStat { get; }
        int RequiredStatValue { get; }
        IReadOnlyList<IAbility> Abilities { get; }

        bool CanEquip(StatBlock stats);
    }
}
