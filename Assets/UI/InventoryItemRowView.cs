using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DungeonTower.UI
{
    /// <summary>
    /// One row in the inventory panel: an item's name and a single
    /// action button. Doesn't know or care what kind of item it
    /// represents — InventoryPanelView configures it generically and
    /// handles the actual equip/use call itself based on which list the
    /// row came from.
    /// </summary>
    public sealed class InventoryItemRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _equipButton;
        [SerializeField] private TextMeshProUGUI _actionButtonLabel; // optional — leave unassigned if the button's own text is static

        // actionLabel lets a row say "Use" (potions/scrolls) instead of
        // "Equip" (weapons/armor); pass null to leave the button's text
        // as whatever it already is (existing callers keep working
        // unchanged).
        public void Configure(string itemLabel, Action onClicked, string actionLabel = null)
        {
            _label.text = itemLabel;
            if (_actionButtonLabel != null && actionLabel != null) _actionButtonLabel.text = actionLabel;
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(() => onClicked?.Invoke());
        }
    }
}
