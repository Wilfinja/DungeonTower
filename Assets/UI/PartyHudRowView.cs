using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// One hero's line in the party HUD: name, HP bar, MP bar, and (in
    /// combat) the actions they have left. Every field is optional except
    /// whatever you want to see, so a prefab can start with just a name and
    /// two sliders. PartyHudView drives it; this only draws.
    /// </summary>
    public sealed class PartyHudRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;

        [Header("Health")]
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("Mana (hidden for heroes with no MP)")]
        [SerializeField] private Slider _mpSlider;
        [SerializeField] private TextMeshProUGUI _mpText;

        [Header("Optional extras")]
        [Tooltip("Shows the actions left this phase while in combat.")]
        [SerializeField] private TextMeshProUGUI _actionsText;
        [Tooltip("Turned on for the hero currently selected.")]
        [SerializeField] private GameObject _selectedHighlight;
        [Tooltip("Dims the whole row when the hero is down.")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Range(0f, 1f)] private float _downAlpha = 0.45f;
        [Tooltip("Click the row to select that hero.")]
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void Refresh(CombatUnit unit, bool selected, bool showActions)
        {
            if (unit == null) return;

            if (_nameLabel != null) _nameLabel.text = unit.DisplayName;

            int maxHp = Mathf.Max(1, unit.Stats.MaxHp);
            if (_hpSlider != null) { _hpSlider.maxValue = maxHp; _hpSlider.value = unit.CurrentHp; }
            if (_hpText != null) _hpText.text = $"HP {unit.CurrentHp}/{unit.Stats.MaxHp}";

            bool hasMp = unit.Stats.MaxMp > 0;
            if (_mpSlider != null)
            {
                _mpSlider.gameObject.SetActive(hasMp);
                _mpSlider.maxValue = Mathf.Max(1, unit.Stats.MaxMp);
                _mpSlider.value = unit.CurrentMp;
            }
            if (_mpText != null)
            {
                _mpText.gameObject.SetActive(hasMp);
                _mpText.text = $"MP {unit.CurrentMp}/{unit.Stats.MaxMp}";
            }

            if (_actionsText != null)
            {
                _actionsText.gameObject.SetActive(showActions && unit.IsAlive);
                _actionsText.text = $"Actions {ActionPoints.Format(unit.Actions.Remaining)}";
            }

            if (_selectedHighlight != null) _selectedHighlight.SetActive(selected && unit.IsAlive);
            if (_group != null) _group.alpha = unit.IsAlive ? 1f : _downAlpha;
            if (_button != null) _button.interactable = unit.IsAlive;
        }
    }
}
