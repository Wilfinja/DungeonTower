using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Core;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Toggleable inventory screen: the party's shared stash as a
    /// scrollable grid, plus ONE party member's gear at a time, switched
    /// with the two member buttons. A member's gear is two separate
    /// grids — a 2-slot equipment row (weapon, armor) and a 4-slot belt
    /// row — so a "Weapon / Armor" and a "Belt" heading can sit next to
    /// each, however they're arranged on screen (stacked or
    /// side-by-side; that's layout, not code). The other member's grids
    /// are fully hidden while not selected, so cross-member drags aren't
    /// possible through the UI — switch tabs, then drag within the
    /// stash. Items are dragged between slots to equip, unequip, load
    /// belts or rearrange the stash; holding Shift while starting a drag
    /// peels one item off a stack instead of moving the whole thing, and
    /// every slot that would accept the current drag is outlined while
    /// it's in flight. Purely a view — it raises MoveRequested (source
    /// slot, target slot, amount) and BattleController decides whether
    /// the move is allowed and what it costs.
    ///
    /// Clicking still works as a shortcut: gear in the stash equips on
    /// whoever's turn it is, and the acting unit's belt potions/scrolls
    /// are used — that's tied to whose turn it is, not which tab is
    /// open. A potion needs an explicit confirm click first, since
    /// unlike a scroll (which still gets aimed at the grid — an easy
    /// point to back out via Escape/right-click before anything is
    /// spent) it resolves immediately: click once for "Confirm
    /// {name}?", click that same slot again to use it. Clicking
    /// anything else drops the pending confirmation.
    ///
    /// In combat, the tab is locked to whoever's turn it is — the other
    /// button is disabled and switching to it is refused even if called
    /// directly. Out of combat either tab is free to view and edit. A
    /// fallen member's button is disabled; if the selected tab's member
    /// falls, or combat starts while the other tab is open, the view
    /// snaps to the acting unit.
    /// </summary>
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [Header("Stash (ScrollRect content with a GridLayoutGroup)")]
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private ScrollRect _scrollRect; // optional — scrolled to the top whenever the panel opens
        [SerializeField, Min(1)] private int _minSlots = 24;

        [Header("Member tabs")]
        [SerializeField] private Button _member1Button;
        [SerializeField] private TextMeshProUGUI _member1Label;
        [SerializeField] private Button _member2Button;
        [SerializeField] private TextMeshProUGUI _member2Label;

        [Header("Member 1 — Equipment (2-slot grid: weapon, armor) and Belt (grid of unit.Belt.SlotCount)")]
        [SerializeField] private Transform _member1EquipmentContainer;
        [SerializeField] private Transform _member1BeltContainer;

        [Header("Member 2 — Equipment (2-slot grid: weapon, armor) and Belt (grid of unit.Belt.SlotCount)")]
        [SerializeField] private Transform _member2EquipmentContainer;
        [SerializeField] private Transform _member2BeltContainer;

        private InventorySlotPool _stashPool;
        private InventorySlotPool _member1EquipmentPool;
        private InventorySlotPool _member1BeltPool;
        private InventorySlotPool _member2EquipmentPool;
        private InventorySlotPool _member2BeltPool;

        private InventorySlotView _ghost;
        private Canvas _ghostCanvas;

        private PartyInventory _inventory;
        private IReadOnlyList<CombatUnit> _partyMembers;
        private bool _inCombat;
        private int _currentIndex;   // whoever's turn it currently is
        private int _selectedIndex;  // which tab is open
        private string _member1Name = "";
        private string _member2Name = "";
        private IPotion _pendingConfirmPotion;

        public event Action<IWeapon, int> WeaponEquipRequested;
        public event Action<IArmor, int> ArmorEquipRequested;
        public event Action<IPotion> PotionUseRequested;
        public event Action<IScroll> ScrollUseRequested;

        // amount is how many to move; int.MaxValue means "the whole
        // stack" (BattleController/InventoryTransfer clamps it).
        public event Action<SlotRef, SlotRef, int> MoveRequested;

        private void Awake()
        {
            if (_member1Button != null) _member1Button.onClick.AddListener(() => SelectMember(0));
            if (_member2Button != null) _member2Button.onClick.AddListener(() => SelectMember(1));
        }

        public void Initialize(PartyInventory inventory)
        {
            _inventory = inventory;
        }

        public void SetPartyMembers(IReadOnlyList<CombatUnit> members)
        {
            _partyMembers = members;
            RefreshTabs();
        }

        public void ConfigurePartyNames(string member1Name, string member2Name)
        {
            _member1Name = member1Name;
            _member2Name = member2Name;
            RefreshTabs();
        }

        // Called whenever a player's turn begins. `locked` is true while
        // any enemy is alerted (in combat); defaultIndex is the acting
        // unit's party index. The belt argument is no longer needed —
        // every member's belt is read straight off the unit — and is
        // only kept so BattleController's call doesn't have to change.
        public void SetTargetMode(bool locked, int defaultIndex, Belt belt)
        {
            _inCombat = locked;
            _currentIndex = defaultIndex;
            _pendingConfirmPotion = null;

            // Combat always views the acting unit. Out of combat, only
            // snap the tab if whatever was selected no longer makes
            // sense (its member fell).
            bool selectedInvalid = _partyMembers != null && _selectedIndex < _partyMembers.Count
                && !_partyMembers[_selectedIndex].IsAlive;
            if (_inCombat || selectedInvalid) _selectedIndex = defaultIndex;

            RefreshTabs();
            if (gameObject.activeSelf) Populate();
        }

        public void Toggle()
        {
            bool willShow = !gameObject.activeSelf;
            gameObject.SetActive(willShow);
            if (willShow)
            {
                Populate();
                if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _pendingConfirmPotion = null;
        }

        // Re-renders in place without changing visibility — used when a
        // change happens for free (out of combat) and the panel should
        // stay open for further changes rather than closing.
        public void Refresh()
        {
            if (gameObject.activeSelf) Populate();
        }

        private void OnDisable()
        {
            EndGhost();
        }

        // ---- Tab switching ------------------------------------------------

        private void SelectMember(int index)
        {
            if (_inCombat && index != _currentIndex) return; // locked to the acting unit
            if (_partyMembers != null && index < _partyMembers.Count && !_partyMembers[index].IsAlive) return;

            _selectedIndex = index;
            _pendingConfirmPotion = null;
            RefreshTabs();
            if (gameObject.activeSelf) Populate();
        }

        private void RefreshTabs()
        {
            SetTab(_member1Button, _member1Label, 0, _member1Name);
            SetTab(_member2Button, _member2Label, 1, _member2Name);
        }

        private void SetTab(Button button, TextMeshProUGUI label, int index, string memberName)
        {
            bool alive = _partyMembers == null || index >= _partyMembers.Count || _partyMembers[index].IsAlive;
            bool locked = _inCombat && index != _currentIndex;

            if (label != null)
            {
                label.text = alive ? memberName : $"{memberName} (fallen)";
                label.fontStyle = index == _selectedIndex ? FontStyles.Bold : FontStyles.Normal;
            }
            if (button != null) button.interactable = alive && !locked;
        }

        // ---- Rendering --------------------------------------------------

        private void Populate()
        {
            EnsurePools();
            RefreshTabs();
            PopulateStash();

            SetContainerActive(_member1EquipmentContainer, _selectedIndex == 0);
            SetContainerActive(_member1BeltContainer, _selectedIndex == 0);
            SetContainerActive(_member2EquipmentContainer, _selectedIndex == 1);
            SetContainerActive(_member2BeltContainer, _selectedIndex == 1);

            if (_selectedIndex == 0)
            {
                PopulateMember(0, _member1EquipmentPool, _member1BeltPool);
                HideMember(_member2EquipmentPool, _member2BeltPool);
            }
            else
            {
                HideMember(_member1EquipmentPool, _member1BeltPool);
                PopulateMember(1, _member2EquipmentPool, _member2BeltPool);
            }
        }

        private static void SetContainerActive(Transform container, bool active)
        {
            if (container != null) container.gameObject.SetActive(active);
        }

        private void EnsurePools()
        {
            if (_stashPool != null) return;

            _stashPool = new InventorySlotPool(_slotContainer, _slotPrefab, OnSlotCreated);
            if (_member1EquipmentContainer != null)
                _member1EquipmentPool = new InventorySlotPool(_member1EquipmentContainer, _slotPrefab, OnSlotCreated);
            if (_member1BeltContainer != null)
                _member1BeltPool = new InventorySlotPool(_member1BeltContainer, _slotPrefab, OnSlotCreated);
            if (_member2EquipmentContainer != null)
                _member2EquipmentPool = new InventorySlotPool(_member2EquipmentContainer, _slotPrefab, OnSlotCreated);
            if (_member2BeltContainer != null)
                _member2BeltPool = new InventorySlotPool(_member2BeltContainer, _slotPrefab, OnSlotCreated);
        }

        private void PopulateStash()
        {
            _stashPool.Begin();

            int slotCount = _inventory != null ? _inventory.SlotCount : 0;
            int columns = ColumnCount();
            int total = Mathf.CeilToInt(Mathf.Max(_minSlots, slotCount) / (float)columns) * columns;

            for (int i = 0; i < total; i++)
            {
                ItemStack stack = _inventory != null ? _inventory.GetSlot(i) : default;
                _stashPool.Next().Configure(BuildStashData(i, stack));
            }

            _stashPool.End();
        }

        private InventorySlotData BuildStashData(int index, ItemStack stack)
        {
            var data = new InventorySlotData { Ref = SlotRef.Stash(index), Draggable = !stack.IsEmpty };
            if (stack.IsEmpty) return data;

            data.Kind = KindOf(stack.Item);
            data.Label = InventoryTransfer.NameOf(stack.Item);
            data.Count = stack.Count;

            // Clicking gear equips it on whoever's turn it is (not
            // necessarily the tab that's open). Stash consumables do
            // nothing on click — they have to be on a belt to be usable.
            if (stack.Item is IWeapon weapon)
                data.OnClick = () => OnEquipClicked(() => WeaponEquipRequested?.Invoke(weapon, _currentIndex));
            else if (stack.Item is IArmor armor)
                data.OnClick = () => OnEquipClicked(() => ArmorEquipRequested?.Invoke(armor, _currentIndex));

            return data;
        }

        // equipmentPool gets exactly 2 slots (weapon, then armor);
        // beltPool gets one slot per unit.Belt.SlotCount.
        private void PopulateMember(int index, InventorySlotPool equipmentPool, InventorySlotPool beltPool)
        {
            if (_partyMembers == null || index >= _partyMembers.Count) return;
            var unit = _partyMembers[index];
            bool editable = unit.IsAlive && (!_inCombat || index == _currentIndex);
            bool acting = unit.IsAlive && index == _currentIndex;

            if (equipmentPool != null)
            {
                equipmentPool.Begin();

                var weapon = unit.EquippedWeapon;
                equipmentPool.Next().Configure(new InventorySlotData
                {
                    Ref = SlotRef.WeaponOf(index),
                    Kind = weapon != null ? InventorySlotKind.Weapon : InventorySlotKind.Empty,
                    Label = weapon != null ? weapon.Name : null,
                    Placeholder = "Weapon",
                    Draggable = editable && weapon != null,
                    Locked = !editable
                });

                var armor = unit.Stats.EquippedArmor;
                equipmentPool.Next().Configure(new InventorySlotData
                {
                    Ref = SlotRef.ArmorOf(index),
                    Kind = armor != null ? InventorySlotKind.Armor : InventorySlotKind.Empty,
                    Label = armor != null ? armor.Name : null,
                    Placeholder = "Armor",
                    Draggable = editable && armor != null,
                    Locked = !editable
                });

                equipmentPool.End();
            }

            if (beltPool != null)
            {
                beltPool.Begin();
                for (int b = 0; b < unit.Belt.SlotCount; b++)
                    beltPool.Next().Configure(BuildBeltData(unit, index, b, editable, acting));
                beltPool.End();
            }
        }

        // Clears every slot in a hidden member's pools so they're
        // excluded from drag highlighting (AllSlots only sees active
        // slots) even though their containers might still technically exist.
        private void HideMember(InventorySlotPool equipmentPool, InventorySlotPool beltPool)
        {
            if (equipmentPool != null) { equipmentPool.Begin(); equipmentPool.End(); }
            if (beltPool != null) { beltPool.Begin(); beltPool.End(); }
        }

        private InventorySlotData BuildBeltData(CombatUnit unit, int memberIndex, int beltSlot, bool editable, bool acting)
        {
            var item = unit.Belt.IsSlotEmpty(beltSlot) ? null : unit.Belt.GetItem(beltSlot);
            var data = new InventorySlotData
            {
                Ref = SlotRef.BeltOf(memberIndex, beltSlot),
                Placeholder = "Belt",
                Draggable = editable && item != null,
                Locked = !editable
            };
            if (item == null) return data;

            data.Kind = KindOf(item);
            data.Label = item.Name;
            data.Count = unit.Belt.GetCount(beltSlot);

            // Only the acting unit's belt is clickable — a potion or
            // scroll always acts on/from whoever's turn it is, whether
            // or not that's the tab currently open.
            if (acting)
            {
                if (item is IPotion potion)
                {
                    bool confirming = ReferenceEquals(_pendingConfirmPotion, potion);
                    data.Pending = confirming;
                    if (confirming) data.Label = $"Confirm {potion.Name}?";
                    data.OnClick = () => OnPotionClicked(potion);
                }
                else if (item is IScroll scroll)
                {
                    data.OnClick = () => OnScrollClicked(scroll);
                }
            }

            return data;
        }

        private int ColumnCount()
        {
            var grid = _slotContainer.GetComponent<GridLayoutGroup>();
            return grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : 1;
        }

        private static InventorySlotKind KindOf(object item)
        {
            if (item is IWeapon) return InventorySlotKind.Weapon;
            if (item is IArmor) return InventorySlotKind.Armor;
            if (item is IPotion) return InventorySlotKind.Potion;
            if (item is IScroll) return InventorySlotKind.Scroll;
            return InventorySlotKind.Empty;
        }

        // ---- Clicks -----------------------------------------------------

        private void OnEquipClicked(Action fireEvent)
        {
            _pendingConfirmPotion = null;
            fireEvent();
        }

        private void OnPotionClicked(IPotion potion)
        {
            if (ReferenceEquals(_pendingConfirmPotion, potion))
            {
                _pendingConfirmPotion = null;
                PotionUseRequested?.Invoke(potion);
            }
            else
            {
                _pendingConfirmPotion = potion;
                Populate();
            }
        }

        private void OnScrollClicked(IScroll scroll)
        {
            _pendingConfirmPotion = null;
            ScrollUseRequested?.Invoke(scroll);
        }

        // ---- Drag and drop ---------------------------------------------

        private void OnSlotCreated(InventorySlotView slot)
        {
            slot.Dropped += OnSlotDropped;
            slot.DragStarted += OnSlotDragStarted;
            slot.Dragging += OnSlotDragging;
            slot.DragEnded += OnSlotDragEnded;
        }

        private void OnSlotDropped(SlotRef from, SlotRef to, int amount)
        {
            _pendingConfirmPotion = null;
            ClearHighlights();
            EndGhost();
            MoveRequested?.Invoke(from, to, amount);
        }

        // The ghost is a copy of the dragged slot parented to the root
        // canvas so it draws above everything, with raycasts off so it
        // never blocks the slot underneath from receiving the drop. It
        // also drives the highlight pass, since both start together.
        private void OnSlotDragStarted(InventorySlotView source, PointerEventData eventData)
        {
            EndGhost();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _ghostCanvas = canvas.rootCanvas;
                _ghost = Instantiate(_slotPrefab, _ghostCanvas.transform);

                var data = source.Data;
                data.OnClick = null;
                data.Draggable = false;
                data.Locked = false;
                data.Pending = false;
                if (source.PendingPartialDrag) data.Count = 1; // show what's actually moving
                _ghost.Configure(data);

                var group = _ghost.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                group.alpha = 0.85f;

                var rect = (RectTransform)_ghost.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = ((RectTransform)source.transform).rect.size;
                rect.SetAsLastSibling();

                MoveGhost(eventData);
            }

            HighlightValidTargets(source.Data.Ref);
        }

        private void OnSlotDragging(InventorySlotView source, PointerEventData eventData)
        {
            MoveGhost(eventData);
        }

        private void OnSlotDragEnded(InventorySlotView source, PointerEventData eventData)
        {
            ClearHighlights();
            EndGhost();
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (_ghost == null || _ghostCanvas == null) return;

            var canvasRect = (RectTransform)_ghostCanvas.transform;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvasRect, eventData.position, eventData.pressEventCamera, out var world))
                _ghost.transform.position = world;
        }

        private void EndGhost()
        {
            if (_ghost == null) return;
            Destroy(_ghost.gameObject);
            _ghost = null;
        }

        // Outlines every visible slot that would accept a drop from
        // `source` right now. Only the selected member's pools have any
        // active slots — the hidden ones were cleared in Populate — so a
        // drag never highlights (or can land in) the other member's gear.
        private void HighlightValidTargets(SlotRef source)
        {
            if (_inventory == null || _partyMembers == null) return;

            foreach (var slot in AllSlots())
            {
                if (slot.Data.Ref.Equals(source)) continue;
                bool ok = InventoryTransfer.CanAccept(_inventory, _partyMembers, source, slot.Data.Ref);
                slot.SetHighlight(ok);
            }
        }

        private void ClearHighlights()
        {
            foreach (var slot in AllSlots())
                slot.SetHighlight(false);
        }

        private IEnumerable<InventorySlotView> AllSlots()
        {
            if (_stashPool != null)
                foreach (var slot in _stashPool.Active()) yield return slot;
            if (_member1EquipmentPool != null)
                foreach (var slot in _member1EquipmentPool.Active()) yield return slot;
            if (_member1BeltPool != null)
                foreach (var slot in _member1BeltPool.Active()) yield return slot;
            if (_member2EquipmentPool != null)
                foreach (var slot in _member2EquipmentPool.Active()) yield return slot;
            if (_member2BeltPool != null)
                foreach (var slot in _member2BeltPool.Active()) yield return slot;
        }
    }
}
