using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Core;
using DungeonTower.Combat;

namespace DungeonTower.UI
{
    /// <summary>
    /// Toggleable panel listing the party's shared gear and consumable
    /// pool, plus a two-member target switcher and a small header
    /// showing whichever member is currently selected their equipped
    /// weapon/armor. Out of combat the switcher is freely clickable so
    /// the whole party's loadout can be reorganized in one sitting; in
    /// combat it locks to whoever's turn it is. Purely a view plus
    /// rendering — BattleController owns whether an equip/use request is
    /// actually allowed and what it costs.
    /// </summary>
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private InventoryItemRowView _rowPrefab;
        [SerializeField] private Button _member1Button;
        [SerializeField] private Button _member2Button;
        [SerializeField] private TextMeshProUGUI _member1Label;
        [SerializeField] private TextMeshProUGUI _member2Label;
        [SerializeField] private TextMeshProUGUI _equippedWeaponLabel;
        [SerializeField] private TextMeshProUGUI _equippedArmorLabel;

        private readonly List<InventoryItemRowView> _spawnedRows = new List<InventoryItemRowView>();
        private PartyInventory _inventory;
        private IReadOnlyList<CombatUnit> _partyMembers;
        private bool _locked;
        private int _selectedTargetIndex;
        private string _member1Name = "";
        private string _member2Name = "";

        public event Action<IWeapon, int> WeaponEquipRequested;
        public event Action<IArmor, int> ArmorEquipRequested;
        public event Action<IPotion, int> PotionUseRequested;
        public event Action<IScroll, int> ScrollUseRequested;

        private void Awake()
        {
            _member1Button.onClick.AddListener(() => SelectTarget(0));
            _member2Button.onClick.AddListener(() => SelectTarget(1));
        }

        public void Initialize(PartyInventory inventory)
        {
            _inventory = inventory;
        }

        public void SetPartyMembers(IReadOnlyList<CombatUnit> members)
        {
            _partyMembers = members;
            RefreshMemberLabels();
        }

        public void ConfigurePartyNames(string member1Name, string member2Name)
        {
            _member1Name = member1Name;
            _member2Name = member2Name;
            RefreshMemberLabels();
        }

        // Called whenever a player's turn begins. defaultIndex is who the
        // switcher starts pointed at; locked=true (any enemy alerted)
        // disables switching, pinning the target to defaultIndex.
        public void SetTargetMode(bool locked, int defaultIndex)
        {
            _locked = locked;
            _selectedTargetIndex = defaultIndex;
            RefreshMemberLabels();
            RefreshEquippedDisplay();
        }

        public void Toggle()
        {
            bool willShow = !gameObject.activeSelf;
            gameObject.SetActive(willShow);
            if (willShow) Populate();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Re-renders in place without changing visibility — used when an
        // equip/use happens for free (out of combat) and the panel
        // should stay open for further changes rather than closing.
        public void Refresh()
        {
            if (gameObject.activeSelf) Populate();
        }

        private void SelectTarget(int index)
        {
            if (_locked) return;
            if (_partyMembers != null && index < _partyMembers.Count && !_partyMembers[index].IsAlive) return;
            _selectedTargetIndex = index;
            RefreshMemberLabels();
            RefreshEquippedDisplay();
        }

        private void RefreshMemberLabels()
        {
            bool member1Alive = _partyMembers == null || _partyMembers.Count <= 0 || _partyMembers[0].IsAlive;
            bool member2Alive = _partyMembers == null || _partyMembers.Count <= 1 || _partyMembers[1].IsAlive;

            _member1Label.text = member1Alive ? _member1Name : $"{_member1Name} (fallen)";
            _member2Label.text = member2Alive ? _member2Name : $"{_member2Name} (fallen)";
            _member1Label.fontStyle = _selectedTargetIndex == 0 ? FontStyles.Bold : FontStyles.Normal;
            _member2Label.fontStyle = _selectedTargetIndex == 1 ? FontStyles.Bold : FontStyles.Normal;

            _member1Button.interactable = !_locked && member1Alive;
            _member2Button.interactable = !_locked && member2Alive;
        }

        private void RefreshEquippedDisplay()
        {
            if (_equippedWeaponLabel == null || _equippedArmorLabel == null) return;

            if (_partyMembers == null || _selectedTargetIndex < 0 || _selectedTargetIndex >= _partyMembers.Count)
            {
                _equippedWeaponLabel.text = "Weapon: —";
                _equippedArmorLabel.text = "Armor: —";
                return;
            }

            var unit = _partyMembers[_selectedTargetIndex];
            _equippedWeaponLabel.text = $"Weapon: {(unit.EquippedWeapon != null ? unit.EquippedWeapon.Name : "— empty —")}";
            _equippedArmorLabel.text = $"Armor: {(unit.Stats.EquippedArmor != null ? unit.Stats.EquippedArmor.Name : "— empty —")}";
        }

        private void Populate()
        {
            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row.gameObject);
            _spawnedRows.Clear();

            RefreshMemberLabels();
            RefreshEquippedDisplay();

            if (_inventory == null) return;

            foreach (var weapon in _inventory.Weapons)
                SpawnRow($"{weapon.Name} (Weapon)", "Equip", () => WeaponEquipRequested?.Invoke(weapon, _selectedTargetIndex));

            foreach (var armor in _inventory.Armors)
                SpawnRow($"{armor.Name} (Armor)", "Equip", () => ArmorEquipRequested?.Invoke(armor, _selectedTargetIndex));

            foreach (var pair in _inventory.Potions)
            {
                var potion = pair.Key;
                SpawnRow($"{potion.Name} x{pair.Value}", "Use", () => PotionUseRequested?.Invoke(potion, _selectedTargetIndex));
            }

            foreach (var pair in _inventory.Scrolls)
            {
                var scroll = pair.Key;
                SpawnRow($"{scroll.Name} x{pair.Value}", "Use", () => ScrollUseRequested?.Invoke(scroll, _selectedTargetIndex));
            }
        }

        private void SpawnRow(string label, string actionLabel, Action onClicked)
        {
            var row = Instantiate(_rowPrefab, _rowContainer);
            row.Configure(label, onClicked, actionLabel);
            _spawnedRows.Add(row);
        }
    }
}
