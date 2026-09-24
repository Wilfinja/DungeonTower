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

        // The weapon's category (Sword, Bow, Staff, ...) — many
        // specific weapons share one Group; this is what lets a passive
        // like "+damage with Swords" match any of them, and what a
        // future loot table would filter by, without each individual
        // weapon needing its own unique enum entry.
        WeaponId Group { get; }
        PrimaryStat RequiredStat { get; }
        int RequiredStatValue { get; }
        IReadOnlyList<IAbility> Abilities { get; }

        // False for a "natural weapon" — a claw, bite, etc. that's
        // intrinsic to the creature rather than a real piece of gear —
        // so it never shows up as loot when that unit dies.
        bool DropsOnDeath { get; }

        bool CanEquip(StatBlock stats);
    }
}
