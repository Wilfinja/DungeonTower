using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// Shows every lootable container currently in range (corpse piles
    /// and/or chests) as click-to-take rows, plus a Take All button.
    /// BattleController decides which containers are in range and what
    /// "taking" an item actually does (adds to PartyInventory, removes
    /// from the container) — this is purely a view + rendering surface,
    /// same division of responsibility as InventoryPanelView.
    /// </summary>
    public sealed class LootWindowView : MonoBehaviour
    {
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private InventoryItemRowView _rowPrefab;
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;

        private readonly List<InventoryItemRowView> _spawnedRows = new List<InventoryItemRowView>();
        private IReadOnlyList<ILootContainer> _containers = Array.Empty<ILootContainer>();

        public event Action<ILootContainer, IWeapon> WeaponTakeRequested;
        public event Action<ILootContainer, IArmor> ArmorTakeRequested;
        public event Action<ILootContainer, IPotion> PotionTakeRequested;
        public event Action<ILootContainer, IScroll> ScrollTakeRequested;
        public event Action TakeAllRequested;
        public event Action CloseRequested;

        private void Awake()
        {
            if (_takeAllButton != null) _takeAllButton.onClick.AddListener(() => TakeAllRequested?.Invoke());
            if (_closeButton != null) _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }

        public void Show(IReadOnlyList<ILootContainer> containers)
        {
            _containers = containers ?? Array.Empty<ILootContainer>();
            gameObject.SetActive(true);
            Populate();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Re-renders in place — call after a take (or after the set of
        // in-range containers changes) so the window reflects what's
        // left without needing to close and reopen it.
        public void Refresh(IReadOnlyList<ILootContainer> containers = null)
        {
            if (containers != null) _containers = containers;
            if (gameObject.activeSelf) Populate();
        }

        private void Populate()
        {
            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row.gameObject);
            _spawnedRows.Clear();

            foreach (var container in _containers)
            {
                foreach (var weapon in container.Weapons)
                    SpawnRow($"{weapon.Name} (Weapon)", () => WeaponTakeRequested?.Invoke(container, weapon));

                foreach (var armor in container.Armors)
                    SpawnRow($"{armor.Name} (Armor)", () => ArmorTakeRequested?.Invoke(container, armor));

                foreach (var pair in container.Potions)
                    SpawnRow($"{pair.Key.Name} x{pair.Value}", () => PotionTakeRequested?.Invoke(container, pair.Key));

                foreach (var pair in container.Scrolls)
                    SpawnRow($"{pair.Key.Name} x{pair.Value}", () => ScrollTakeRequested?.Invoke(container, pair.Key));
            }
        }

        private void SpawnRow(string label, Action onClicked)
        {
            var row = Instantiate(_rowPrefab, _rowContainer);
            row.Configure(label, onClicked, "Take");
            _spawnedRows.Add(row);
        }
    }
}
