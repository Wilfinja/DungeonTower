using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    public enum InventorySlotKind { Empty, Weapon, Armor, Potion, Scroll }

    /// <summary>Everything a slot needs to draw itself and behave.</summary>
    public struct InventorySlotData
    {
        public SlotRef Ref;
        public InventorySlotKind Kind;
        public string Label;
        public string Placeholder;   // dim text shown while empty ("Weapon", "Armor", "Belt")
        public int Count;
        public Action OnClick;
        public bool Pending;         // "click again to confirm" highlight
        public bool Draggable;
        public bool Locked;          // greyed out and unclickable (not this unit's turn, fallen, ...)
    }

    /// <summary>
    /// One cell anywhere in the inventory UI — a stash cell, an
    /// equipment slot, or a belt slot. Shows an item's name, an optional
    /// stack count and a kind-based tint, fires a click action, and can
    /// be dragged onto another slot. Holding Shift while starting a drag
    /// moves exactly one item off a stack instead of the whole stack —
    /// PendingPartialDrag reports that to whoever reads it, and the
    /// panel forwards it as the drop's amount. Items have no icons yet,
    /// so the name stands in for one.
    ///
    /// It only reports what happened — SlotRef source/target and the
    /// amount — and what a move means is decided elsewhere. SetHighlight
    /// lets the panel mark this slot as a valid/invalid drop target
    /// while something is being dragged.
    ///
    /// A slot that isn't carrying an item drag forwards its drag events
    /// to the parent ScrollRect (if any), so dragging over empty or
    /// locked cells still scrolls the stash.
    /// </summary>
    public sealed class InventorySlotView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private TextMeshProUGUI _countLabel; // optional
        [SerializeField] private Image _highlightOutline;     // optional — a border/glow Image, off by default

        [Header("Tints")]
        [SerializeField] private Color _emptyTint = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color _weaponTint = new Color(0.85f, 0.45f, 0.35f, 1f);
        [SerializeField] private Color _armorTint = new Color(0.40f, 0.60f, 0.85f, 1f);
        [SerializeField] private Color _potionTint = new Color(0.45f, 0.80f, 0.50f, 1f);
        [SerializeField] private Color _scrollTint = new Color(0.80f, 0.70f, 0.40f, 1f);
        [SerializeField] private Color _pendingTint = new Color(1f, 0.9f, 0.3f, 1f);

        [Header("Opacity")]
        [SerializeField, Range(0f, 1f)] private float _lockedAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _draggingAlpha = 0.4f;

        private CanvasGroup _canvasGroup;
        private ScrollRect _scroll;
        private bool _dragging;

        public InventorySlotData Data { get; private set; }

        // True for the duration of a drag that was started with Shift
        // held — a request to move just one item off the stack. Read by
        // the slot being dropped ON, off the slot being dropped FROM.
        public bool PendingPartialDrag { get; private set; }

        // (source, target, amount) — amount is how many to move;
        // int.MaxValue means "the whole stack" (the panel clamps it to
        // what's actually there). Raised on the slot something was
        // dropped ON.
        public event Action<SlotRef, SlotRef, int> Dropped;
        public event Action<InventorySlotView, PointerEventData> DragStarted;
        public event Action<InventorySlotView, PointerEventData> Dragging;
        public event Action<InventorySlotView, PointerEventData> DragEnded;

        private void Awake()
        {
            EnsureRefs();
        }

        private void OnDisable()
        {
            // A deactivated slot never receives OnEndDrag, so don't
            // leave it stuck mid-drag, and don't leave a stale highlight.
            _dragging = false;
            SetHighlight(false);
        }

        // Safe to call repeatedly on a reused slot.
        public void Configure(InventorySlotData data)
        {
            EnsureRefs();
            Data = data;
            bool empty = data.Kind == InventorySlotKind.Empty;

            _label.text = empty ? (data.Placeholder ?? "") : (data.Label ?? "");
            _label.alpha = empty ? 0.4f : 1f;
            if (_countLabel != null) _countLabel.text = data.Count > 1 ? $"x{data.Count}" : "";
            if (_background != null) _background.color = data.Pending ? _pendingTint : TintFor(data.Kind);

            _button.interactable = !empty && !data.Locked;
            _button.onClick.RemoveAllListeners();
            var onClick = data.OnClick;
            if (onClick != null) _button.onClick.AddListener(() => onClick());

            SetHighlight(false);
            ApplyAlpha();
        }

        // Toggles the drop-target outline. Called by the panel while a
        // drag is in flight; has no other effect on the slot.
        public void SetHighlight(bool visible)
        {
            if (_highlightOutline != null) _highlightOutline.enabled = visible;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Data.Draggable)
            {
                if (_scroll != null) _scroll.OnBeginDrag(eventData);
                return;
            }

            _dragging = true;
            PendingPartialDrag = Data.Count > 1 && Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            ApplyAlpha();
            DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging) Dragging?.Invoke(this, eventData);
            else if (_scroll != null) _scroll.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragging)
            {
                _dragging = false;
                ApplyAlpha();
                DragEnded?.Invoke(this, eventData);
            }
            else if (_scroll != null)
            {
                _scroll.OnEndDrag(eventData);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;
            var source = eventData.pointerDrag.GetComponentInParent<InventorySlotView>();
            if (source == null || source == this) return;

            int amount = source.PendingPartialDrag ? 1 : int.MaxValue;
            Dropped?.Invoke(source.Data.Ref, Data.Ref, amount);
        }

        private void EnsureRefs()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            if (_scroll == null) _scroll = GetComponentInParent<ScrollRect>(true);
        }

        private void ApplyAlpha()
        {
            _canvasGroup.alpha = _dragging ? _draggingAlpha : (Data.Locked ? _lockedAlpha : 1f);
        }

        private Color TintFor(InventorySlotKind kind)
        {
            switch (kind)
            {
                case InventorySlotKind.Weapon: return _weaponTint;
                case InventorySlotKind.Armor: return _armorTint;
                case InventorySlotKind.Potion: return _potionTint;
                case InventorySlotKind.Scroll: return _scrollTint;
                default: return _emptyTint;
            }
        }
    }
}
