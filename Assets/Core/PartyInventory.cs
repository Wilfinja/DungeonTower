using System.Collections.Generic;

namespace DungeonTower.Core
{
    /// <summary>
    /// The party's shared pool of unequipped gear. Deliberately simple —
    /// two flat lists, no stacking, no sorting. Weapon/Armor instances from
    /// WeaponLibrary/ArmorLibrary are shared singletons already (every
    /// sword-wielding Grunt references the same Weapon object) — holding
    /// one in the party pool while another copy is equipped elsewhere is
    /// consistent with that, not a bug.
    /// </summary>
    public sealed class PartyInventory
    {
        private readonly List<Weapon> _weapons = new List<Weapon>();
        private readonly List<Armor> _armors = new List<Armor>();

        public IReadOnlyList<Weapon> Weapons => _weapons;
        public IReadOnlyList<Armor> Armors => _armors;

        public void AddWeapon(Weapon weapon)
        {
            if (weapon != null) _weapons.Add(weapon);
        }

        public void AddArmor(Armor armor)
        {
            if (armor != null) _armors.Add(armor);
        }

        public bool RemoveWeapon(Weapon weapon) => _weapons.Remove(weapon);
        public bool RemoveArmor(Armor armor) => _armors.Remove(armor);
    }
}
