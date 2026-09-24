using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// Scene-level lookup from an item's ID enum to its actual asset.
    /// Populate the four lists in the Inspector; everything that used to
    /// reach for the old static WeaponLibrary/ArmorLibrary (or would
    /// otherwise hold a direct SO reference) should hold a serialized
    /// reference to this instead and call the matching Get method.
    ///
    /// One of these is expected to exist in the scene. Building the
    /// lookup dictionaries happens in Awake, so make sure this component
    /// runs before anything else calls a Get method (Script Execution
    /// Order, or just place it high in the hierarchy — Unity runs Awake
    /// on all objects before any Start).
    /// </summary>
    public sealed class ItemRegistry : MonoBehaviour
    {
        [SerializeField] private List<WeaponSO> _weapons = new List<WeaponSO>();
        [SerializeField] private List<ArmorSO> _armor = new List<ArmorSO>();
        [SerializeField] private List<PotionSO> _potions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _scrolls = new List<ScrollSO>();

        private readonly Dictionary<WeaponId, IWeapon> _weaponsById = new Dictionary<WeaponId, IWeapon>();
        private readonly Dictionary<ArmorId, IArmor> _armorById = new Dictionary<ArmorId, IArmor>();
        private readonly Dictionary<PotionId, PotionSO> _potionsById = new Dictionary<PotionId, PotionSO>();
        private readonly Dictionary<ScrollId, ScrollSO> _scrollsById = new Dictionary<ScrollId, ScrollSO>();

        private void Awake()
        {
            Index(_weapons, w => w.Group, _weaponsById, "WeaponId");
            Index(_armor, a => a.Group, _armorById, "ArmorId");
            Index(_potions, p => p.Group, _potionsById, "PotionId");
            Index(_scrolls, s => s.Group, _scrollsById, "ScrollId");
        }

        // TValue is deliberately separate from TAsset: weapons/armor are
        // indexed into Dictionary<TId, IWeapon/IArmor> (the Core
        // interface) even though the source list is the concrete SO
        // type, while potions/scrolls are indexed straight into their
        // own concrete SO type. The "where TAsset : TValue" constraint
        // is what makes a WeaponSO valid to store as an IWeapon without
        // forcing the list and dictionary to agree on one exact type —
        // that mismatch is exactly what made type inference fail before.
        private void Index<TAsset, TId, TValue>(
            List<TAsset> assets, System.Func<TAsset, TId> idOf, Dictionary<TId, TValue> into, string idLabel)
            where TAsset : UnityEngine.Object, TValue
        {
            into.Clear();
            foreach (var asset in assets)
            {
                if (asset == null) continue;
                var id = idOf(asset);
                if (into.ContainsKey(id))
                {
                    Debug.LogWarning($"ItemRegistry: duplicate {idLabel} {id} on '{asset.name}' — ignoring.", this);
                    continue;
                }
                into.Add(id, asset);
            }
        }

        public IWeapon GetWeapon(WeaponId id)
        {
            if (_weaponsById.TryGetValue(id, out var weapon)) return weapon;
            Debug.LogError($"ItemRegistry: no WeaponSO registered for {id}. Check the registry's Weapons list.");
            return null;
        }

        public IArmor GetArmor(ArmorId id)
        {
            if (_armorById.TryGetValue(id, out var armor)) return armor;
            Debug.LogError($"ItemRegistry: no ArmorSO registered for {id}. Check the registry's Armor list.");
            return null;
        }

        // Returned as the Core interface, same as GetWeapon/GetArmor —
        // callers outside Items never need to know the concrete SO type.
        public IPotion GetPotion(PotionId id)
        {
            if (_potionsById.TryGetValue(id, out var potion)) return potion;
            Debug.LogError($"ItemRegistry: no PotionSO registered for {id}. Check the registry's Potions list.");
            return null;
        }

        public IScroll GetScroll(ScrollId id)
        {
            if (_scrollsById.TryGetValue(id, out var scroll)) return scroll;
            Debug.LogError($"ItemRegistry: no ScrollSO registered for {id}. Check the registry's Scrolls list.");
            return null;
        }
    }
}
