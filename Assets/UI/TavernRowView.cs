using System;
using System.Text;
using DungeonTower.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// One hero type's row in the tavern list: name, stats, a line about
    /// their starting gear, and one action button (Recruit / Add / Remove).
    /// TavernView fills it in. Put this on the row prefab.
    /// </summary>
    public sealed class TavernRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _statsLabel;
        [Tooltip("Description and starting gear.")]
        [SerializeField] private TextMeshProUGUI _detailLabel;
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        public void Bind(HeroTypeSO type, string actionText, bool interactable, Action onClick)
        {
            if (_nameLabel != null) _nameLabel.text = type.DisplayName;

            var stats = type.Stats;
            if (_statsLabel != null)
                _statsLabel.text = $"Body {stats.Body}   Mind {stats.Mind}   Spirit {stats.Spirit}";

            if (_detailLabel != null) _detailLabel.text = Describe(type);

            if (_actionLabel != null) _actionLabel.text = actionText;
            if (_actionButton != null)
            {
                _actionButton.interactable = interactable;
                _actionButton.onClick.RemoveAllListeners();
                _actionButton.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        private static string Describe(HeroTypeSO type)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(type.Description)) sb.Append(type.Description).Append('\n');

            var gear = new System.Collections.Generic.List<string>();
            if (type.Weapon != null) gear.Add(type.Weapon.Name);
            if (type.OffHand != null) gear.Add(type.OffHand.Name);
            if (type.Armor != null) gear.Add(type.Armor.Name);
            sb.Append("Starts with: ").Append(gear.Count > 0 ? string.Join(", ", gear) : "nothing");
            return sb.ToString();
        }
    }
}
