using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Combat;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// The character sheet: one hero at a time (switch with the member
    /// tabs) showing name, level, XP toward the next level, HP/MP, their
    /// Body/Mind/Spirit (hover for what each affects), derived combat
    /// numbers, and what they have equipped with full details. Whenever a
    /// hero has unspent stat points, a "+" button appears beside each stat.
    ///
    /// Purely a view, same as InventoryPanelView: it raises
    /// SpendPointRequested and BattleController applies it. While open it
    /// watches the selected hero's numbers and redraws itself only when
    /// something visible changed, so nothing else needs to tell it to
    /// refresh (Refresh() exists to force one anyway).
    /// </summary>
    public sealed class CharacterSheetView : MonoBehaviour
    {
        [Serializable]
        public sealed class MemberTab
        {
            public Button Button;
            public TextMeshProUGUI Label;
            [Tooltip("Optional. Shown while this hero has unspent stat points.")]
            public GameObject LevelUpBadge;
        }

        [Serializable]
        public sealed class StatRow
        {
            public PrimaryStat Stat;
            [Tooltip("Shows '<Stat> <value>' and carries the hover tooltip. Needs Raycast Target on.")]
            public TextMeshProUGUI Label;
            [Tooltip("The '+' button. Hidden whenever the hero has no unspent points.")]
            public Button PlusButton;
        }

        [Header("Member tabs (one per hero, in party order)")]
        [SerializeField] private List<MemberTab> _tabs = new List<MemberTab>();

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _levelLabel;

        [Header("XP")]
        [SerializeField] private TextMeshProUGUI _xpLabel;
        [Tooltip("Optional. An Image with Image Type = Filled.")]
        [SerializeField] private Image _xpFill;

        [Header("Vitals")]
        [SerializeField] private TextMeshProUGUI _hpLabel;
        [SerializeField] private Image _hpFill;   // optional, Image Type = Filled
        [SerializeField] private TextMeshProUGUI _mpLabel;
        [SerializeField] private Image _mpFill;   // optional, Image Type = Filled

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI _pointsLabel;
        [SerializeField] private List<StatRow> _statRows = new List<StatRow>();
        [Tooltip("Attack, defense, initiative, move range, resist, crit — one line each.")]
        [SerializeField] private TextMeshProUGUI _derivedLabel;

        [Header("Equipment")]
        [SerializeField] private TextMeshProUGUI _weaponNameLabel;
        [SerializeField] private TextMeshProUGUI _weaponDetailsLabel;
        [SerializeField] private TextMeshProUGUI _armorNameLabel;
        [SerializeField] private TextMeshProUGUI _armorDetailsLabel;

        [Header("Other")]
        [SerializeField] private Button _closeButton;

        private IReadOnlyList<CombatUnit> _members;
        private int _selectedIndex;
        private int _lastSignature = int.MinValue;

        // (party index, stat) — the panel never changes stats itself.
        public event Action<int, PrimaryStat> SpendPointRequested;

        public bool IsOpen => gameObject.activeSelf;

        private CombatUnit SelectedUnit
            => _members != null && _selectedIndex >= 0 && _selectedIndex < _members.Count
                ? _members[_selectedIndex] : null;

        private void Awake()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                if (_tabs[i].Button != null) _tabs[i].Button.onClick.AddListener(() => SelectMember(index));
            }

            foreach (var row in _statRows)
            {
                var captured = row;
                if (captured.PlusButton != null)
                    captured.PlusButton.onClick.AddListener(() => SpendPointRequested?.Invoke(_selectedIndex, captured.Stat));
                if (captured.Label != null)
                    TooltipTrigger.Attach(captured.Label.gameObject, () =>
                    {
                        var unit = SelectedUnit;
                        return unit != null ? StatDescriptions.Describe(captured.Stat, unit.Stats) : null;
                    });
            }

            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            _lastSignature = int.MinValue;
            Rebuild();
        }

        // ---- Public API ---------------------------------------------------

        public void SetPartyMembers(IReadOnlyList<CombatUnit> members)
        {
            _members = members;
            _lastSignature = int.MinValue;
        }

        public void Show(int memberIndex)
        {
            _selectedIndex = memberIndex;
            EnsureSelectedIsAlive();
            gameObject.SetActive(true);   // OnEnable draws it
            Refresh();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (TooltipView.Instance != null) TooltipView.Instance.Hide();
        }

        public void Refresh()
        {
            _lastSignature = int.MinValue;
            if (isActiveAndEnabled) Rebuild();
        }

        // ---- Redraw only when something visible changed ------------------------

        private void LateUpdate()
        {
            EnsureSelectedIsAlive();
            int signature = ComputeSignature();
            if (signature == _lastSignature) return;
            Rebuild();
        }

        private void SelectMember(int index)
        {
            if (_members == null || index < 0 || index >= _members.Count) return;
            if (!_members[index].IsAlive) return;

            _selectedIndex = index;
            Refresh();
        }

        // If the open hero falls, hop to the first hero still standing.
        private void EnsureSelectedIsAlive()
        {
            if (_members == null || _members.Count == 0) return;
            if (_selectedIndex >= 0 && _selectedIndex < _members.Count && _members[_selectedIndex].IsAlive) return;

            for (int i = 0; i < _members.Count; i++)
            {
                if (!_members[i].IsAlive) continue;
                _selectedIndex = i;
                return;
            }
        }

        private int ComputeSignature()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + _selectedIndex;

                if (_members != null)
                {
                    foreach (var member in _members)
                    {
                        h = h * 31 + (member.IsAlive ? 1 : 0);
                        h = h * 31 + member.Stats.UnspentPoints;
                        h = h * 31 + (member.DisplayName != null ? member.DisplayName.GetHashCode() : 0);
                    }
                }

                var unit = SelectedUnit;
                if (unit == null) return h;

                var s = unit.Stats;
                h = h * 31 + s.Level;
                h = h * 31 + s.Experience;
                h = h * 31 + s.Body;
                h = h * 31 + s.Mind;
                h = h * 31 + s.Spirit;
                h = h * 31 + unit.CurrentHp;
                h = h * 31 + unit.CurrentMp;
                h = h * 31 + s.MaxHp;
                h = h * 31 + s.MaxMp;
                h = h * 31 + Mathf.RoundToInt(s.PhysicalAttack * 10f);
                h = h * 31 + Mathf.RoundToInt(s.PhysicalDefense * 10f);
                h = h * 31 + Mathf.RoundToInt(s.MagicAttack * 10f);
                h = h * 31 + Mathf.RoundToInt(s.MagicDefense * 10f);
                h = h * 31 + s.Initiative;
                h = h * 31 + s.MoveRange;
                h = h * 31 + Mathf.RoundToInt(s.StatusResist * 10f);
                h = h * 31 + Mathf.RoundToInt(s.CritChance * 10f);
                h = h * 31 + (unit.EquippedWeapon != null ? unit.EquippedWeapon.GetHashCode() : 0);
                h = h * 31 + (s.EquippedArmor != null ? s.EquippedArmor.GetHashCode() : 0);
                return h;
            }
        }

        // ---- Rendering ----------------------------------------------------------

        private void Rebuild()
        {
            RebuildTabs();

            var unit = SelectedUnit;
            _lastSignature = ComputeSignature();
            if (unit == null) return;

            var s = unit.Stats;

            SetText(_nameLabel, unit.DisplayName);
            SetText(_levelLabel, $"Level {s.Level}");

            SetText(_xpLabel, $"XP {s.Experience} / {s.ExperienceToNextLevel}");
            SetFill(_xpFill, s.Experience, s.ExperienceToNextLevel);

            SetText(_hpLabel, $"HP {unit.CurrentHp} / {s.MaxHp}");
            SetFill(_hpFill, unit.CurrentHp, s.MaxHp);
            SetText(_mpLabel, $"MP {unit.CurrentMp} / {s.MaxMp}");
            SetFill(_mpFill, unit.CurrentMp, s.MaxMp);

            bool canSpend = unit.IsAlive && s.UnspentPoints > 0;
            SetText(_pointsLabel, s.UnspentPoints > 0
                ? UiText.Highlight($"Points to spend: {s.UnspentPoints}")
                : "Points to spend: 0");

            foreach (var row in _statRows)
            {
                SetText(row.Label, $"{row.Stat} {s.Current.Get(row.Stat)}");
                if (row.PlusButton != null) row.PlusButton.gameObject.SetActive(canSpend);
            }

            SetText(_derivedLabel,
                $"Physical Attack: {UiText.Num(s.PhysicalAttack)}\n" +
                $"Physical Defense: {UiText.Num(s.PhysicalDefense)}\n" +
                $"Magic Attack: {UiText.Num(s.MagicAttack)}\n" +
                $"Magic Defense: {UiText.Num(s.MagicDefense)}\n" +
                $"Initiative: {s.Initiative}\n" +
                $"Move Range: {s.MoveRange}\n" +
                $"Status Resist: {UiText.Num(s.StatusResist)}%\n" +
                $"Crit Chance: {UiText.Num(s.CritChance)}%");

            var weapon = unit.EquippedWeapon;
            SetText(_weaponNameLabel, weapon != null ? weapon.Name : "No weapon");
            SetText(_weaponDetailsLabel, weapon != null ? ItemDescriber.Describe(weapon, s) : "");

            var armor = s.EquippedArmor;
            SetText(_armorNameLabel, armor != null ? armor.Name : "No armor");
            SetText(_armorDetailsLabel, armor != null ? ItemDescriber.Describe(armor, s) : "");
        }

        private void RebuildTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool exists = _members != null && i < _members.Count;

                if (tab.Button != null) tab.Button.gameObject.SetActive(exists);
                if (!exists) continue;

                var member = _members[i];
                if (tab.Button != null) tab.Button.interactable = member.IsAlive;
                if (tab.Label != null)
                {
                    tab.Label.text = member.IsAlive ? member.DisplayName : $"{member.DisplayName} (fallen)";
                    tab.Label.fontStyle = i == _selectedIndex ? FontStyles.Bold : FontStyles.Normal;
                }
                if (tab.LevelUpBadge != null)
                    tab.LevelUpBadge.SetActive(member.IsAlive && member.Stats.UnspentPoints > 0);
            }
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }

        private static void SetFill(Image fill, int value, int max)
        {
            if (fill != null) fill.fillAmount = max > 0 ? Mathf.Clamp01(value / (float)max) : 0f;
        }
    }
}
