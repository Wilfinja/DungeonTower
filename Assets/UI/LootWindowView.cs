using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DungeonTower.Core;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Shows every lootable container currently in range (corpse piles
    /// and/or chests) as a grid of the same boxes the inventory uses, plus
    /// a Take All button. Click a box to take that item (or one copy of a
    /// stack); hover it for the item's details. Empty boxes pad the grid
    /// out to a minimum size, so the window looks like the stash instead
    /// of collapsing to a few cells.
    /// BattleController decides which containers are in range and what
    /// "taking" an item actually does (adds to PartyInventory, removes
    /// from the container) — this is purely a view + rendering surface,
    /// same division of responsibility as InventoryPanelView.
    /// </summary>
    public sealed class LootWindowView : MonoBehaviour
    {
        [Header("Grid (a container with a GridLayoutGroup)")]
        // Was the row list's container; the old reference carries over.
        [FormerlySerializedAs("_rowContainer")]
        [SerializeField] private Transform _slotContainer;
        [Tooltip("The same slot prefab the inventory stash uses.")]
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private ScrollRect _scrollRect; // optional — scrolled to the top whenever the window opens
        [SerializeField, Min(1)] private int _minSlots = 12;

        [Header("Buttons")]
        [SerializeField] private Button _takeAllButton;
        [SerializeField] private Button _closeButton;

        private InventorySlotPool _pool;
        // Each slot is reconfigured in place, so its hover text is looked
        // up by slot rather than baked into the (one-time) tooltip trigger.
        private readonly Dictionary<InventorySlotView, Func<string>> _tooltips = new Dictionary<InventorySlotView, Func<string>>();
        private IReadOnlyList<ILootContainer> _containers = Array.Empty<ILootContainer>();
        private UnitStats _viewer;

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

        // `viewer` is whoever is looting; it makes the hover text show
        // their numbers and which requirements they meet. Optional.
        public void Show(IReadOnlyList<ILootContainer> containers, UnitStats viewer = null)
        {
            _containers = containers ?? Array.Empty<ILootContainer>();
            _viewer = viewer;
            gameObject.SetActive(true);
            Populate();
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Re-renders in place — call after a take (or after the set of
        // in-range containers changes) so the window reflects what's
        // left without needing to close and reopen it.
        public void Refresh(IReadOnlyList<ILootContainer> containers = null, UnitStats viewer = null)
        {
            if (containers != null) _containers = containers;
            if (viewer != null) _viewer = viewer;
            if (gameObject.activeSelf) Populate();
        }

        private void Populate()
        {
            if (_slotContainer == null || _slotPrefab == null)
            {
                Debug.LogError("LootWindowView: assign the Slot Container and Slot Prefab in the Inspector.");
                return;
            }

            if (_pool == null) _pool = new InventorySlotPool(_slotContainer, _slotPrefab, OnSlotCreated);
            _pool.Begin();
            int used = 0;

            foreach (var container in _containers)
            {
                foreach (var weapon in container.Weapons)
                {
                    var captured = container;
                    var item = weapon;
                    Fill(InventorySlotKind.Weapon, item.Name, 1,
                        () => WeaponTakeRequested?.Invoke(captured, item),
                        () => ItemDescriber.Describe(item, _viewer));
                    used++;
                }

                foreach (var armor in container.Armors)
                {
                    var captured = container;
                    var item = armor;
                    Fill(InventorySlotKind.Armor, item.Name, 1,
                        () => ArmorTakeRequested?.Invoke(captured, item),
                        () => ItemDescriber.Describe(item, _viewer));
                    used++;
                }

                foreach (var pair in container.Potions)
                {
                    var captured = container;
                    var item = pair.Key;
                    Fill(InventorySlotKind.Potion, item.Name, pair.Value,
                        () => PotionTakeRequested?.Invoke(captured, item),
                        () => DescribeConsumable(item, item.Ability));
                    used++;
                }

                foreach (var pair in container.Scrolls)
                {
                    var captured = container;
                    var item = pair.Key;
                    Fill(InventorySlotKind.Scroll, item.Name, pair.Value,
                        () => ScrollTakeRequested?.Invoke(captured, item),
                        () => DescribeConsumable(item, item.Ability));
                    used++;
                }
            }

            // Pad with empty boxes to a minimum size, rounded up to whole
            // rows so the last row isn't ragged — same look as the stash.
            int columns = ColumnCount();
            int total = Mathf.CeilToInt(Mathf.Max(_minSlots, used) / (float)columns) * columns;
            for (int i = used; i < total; i++)
                Fill(InventorySlotKind.Empty, null, 0, null, null);

            _pool.End();
        }

        private void Fill(InventorySlotKind kind, string label, int count, Action onClicked, Func<string> tooltip)
        {
            var slot = _pool.Next();
            slot.Configure(new InventorySlotData
            {
                Kind = kind,
                Label = label,
                Count = count,
                OnClick = onClicked,
                Draggable = false   // taking is click-only; drags just scroll the grid
            });
            _tooltips[slot] = tooltip;
        }

        // Item name and requirement, then what its ability does.
        private string DescribeConsumable(IConsumable item, IAbility ability)
        {
            return "<b>" + item.Name + "</b>\n"
                + ItemDescriber.RequirementLine(item.RequiredStat, item.RequiredStatValue, _viewer) + "\n"
                + ItemDescriber.Describe(ability, _viewer);
        }

        private void OnSlotCreated(InventorySlotView slot)
        {
            TooltipTrigger.Attach(slot.gameObject, () =>
                _tooltips.TryGetValue(slot, out var provider) && provider != null ? provider() : "");
        }

        private int ColumnCount()
        {
            var grid = _slotContainer.GetComponent<GridLayoutGroup>();
            return grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : 1;
        }
    }
}
