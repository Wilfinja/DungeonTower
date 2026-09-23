using System.Collections.Generic;
using DungeonTower.Core;
using UnityEngine;

namespace DungeonTower.Items
{
    /// <summary>
    /// A placed, persistent world object with hand-authored contents —
    /// unlike LootDrop (created at runtime when a unit dies), a chest's
    /// loot is set once in the Inspector. Taking an item removes it for
    /// good; an emptied chest just sits there with nothing left to take.
    ///
    /// GridX/GridY are set by hand to match wherever the chest is placed
    /// in the scene, rather than derived from the transform — deriving
    /// it would need GridToWorld's conversion, which lives in
    /// DungeonTower.UI, and Items can't reference UI without creating a
    /// circular assembly reference (UI already references Items). Keep
    /// the chest's transform position and its GridX/GridY in sync by
    /// hand when placing one.
    /// </summary>
    public sealed class Chest : MonoBehaviour, ILootContainer
    {
        [SerializeField] private int _gridX;
        [SerializeField] private int _gridY;
        [SerializeField] private List<WeaponSO> _weapons = new List<WeaponSO>();
        [SerializeField] private List<ArmorSO> _armor = new List<ArmorSO>();
        [SerializeField] private List<PotionSO> _potions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _scrolls = new List<ScrollSO>();

        private readonly Dictionary<IPotion, int> _potionCounts = new Dictionary<IPotion, int>();
        private readonly Dictionary<IScroll, int> _scrollCounts = new Dictionary<IScroll, int>();

        public GridPosition Position => new GridPosition(_gridX, _gridY);

        // On it or adjacent — a chest is a fixed piece of furniture, not
        // something you have to stand exactly on top of.
        public int InteractionRadius => 1;

        private void Awake()
        {
            RebuildCounts();
        }

        private void RebuildCounts()
        {
            _potionCounts.Clear();
            foreach (var potion in _potions)
            {
                if (potion == null) continue;
                _potionCounts.TryGetValue(potion, out var count);
                _potionCounts[potion] = count + 1;
            }

            _scrollCounts.Clear();
            foreach (var scroll in _scrolls)
            {
                if (scroll == null) continue;
                _scrollCounts.TryGetValue(scroll, out var count);
                _scrollCounts[scroll] = count + 1;
            }
        }

        public bool IsEmpty => _weapons.Count == 0 && _armor.Count == 0
            && _potionCounts.Count == 0 && _scrollCounts.Count == 0;

        public IReadOnlyList<IWeapon> Weapons => _weapons;
        public IReadOnlyList<IArmor> Armors => _armor;
        public IReadOnlyDictionary<IPotion, int> Potions => _potionCounts;
        public IReadOnlyDictionary<IScroll, int> Scrolls => _scrollCounts;

        public void TakeWeapon(IWeapon weapon)
        {
            if (weapon is WeaponSO so) _weapons.Remove(so);
        }

        public void TakeArmor(IArmor armor)
        {
            if (armor is ArmorSO so) _armor.Remove(so);
        }

        public void TakePotion(IPotion potion)
        {
            if (!(potion is PotionSO so)) return;
            if (!_potionCounts.TryGetValue(potion, out var count) || count <= 0) return;
            _potions.Remove(so); // removes exactly one instance from the backing list
            if (count == 1) _potionCounts.Remove(potion);
            else _potionCounts[potion] = count - 1;
        }

        public void TakeScroll(IScroll scroll)
        {
            if (!(scroll is ScrollSO so)) return;
            if (!_scrollCounts.TryGetValue(scroll, out var count) || count <= 0) return;
            _scrolls.Remove(so);
            if (count == 1) _scrollCounts.Remove(scroll);
            else _scrollCounts[scroll] = count - 1;
        }
    }
}
