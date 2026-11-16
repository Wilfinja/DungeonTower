using System;
using System.Collections.Generic;
using DungeonTower.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// The tavern: every hero type in the game as a row. Unlocked types can
    /// be added to (or removed from) your party of three; locked types can
    /// be recruited for gems. The party you pick is remembered in the town
    /// profile and is what the next run starts with.
    ///
    /// Put this on the tavern panel. It refreshes itself whenever the panel
    /// is shown. TownFlow reads Party / PartyReady to start a run.
    /// </summary>
    public sealed class TavernView : MonoBehaviour
    {
        public const int PartySize = 3;

        [SerializeField] private HeroTypeRegistrySO _registry;

        [Header("Roster list")]
        [SerializeField] private TavernRowView _rowPrefab;
        [Tooltip("Rows are created here. A Vertical Layout Group works well.")]
        [SerializeField] private Transform _rowContainer;

        [Header("Party (three slots, in order)")]
        [SerializeField] private List<TextMeshProUGUI> _partySlotLabels = new List<TextMeshProUGUI>();
        [Tooltip("One per slot: empties that slot.")]
        [SerializeField] private List<Button> _partySlotClearButtons = new List<Button>();

        [SerializeField] private TextMeshProUGUI _gemsLabel;
        [Tooltip("Optional. Says why something didn't work (not enough gems, party full).")]
        [SerializeField] private TextMeshProUGUI _messageLabel;

        [Tooltip("Allow the same hero type more than once in the party. Off keeps each unit's letter on the map distinct.")]
        [SerializeField] private bool _allowDuplicateTypes;

        private readonly List<HeroTypeSO> _party = new List<HeroTypeSO>();
        private bool _loaded;

        /// <summary>The chosen heroes, in slot order (empty slots are skipped).</summary>
        public IReadOnlyList<HeroTypeSO> Party
        {
            get { EnsureLoaded(); return _party; }
        }

        public bool PartyReady
        {
            get { EnsureLoaded(); return _party.Count == PartySize; }
        }

        /// <summary>Raised whenever the party changes.</summary>
        public event Action PartyChanged;

        private void Awake()
        {
            for (int i = 0; i < _partySlotClearButtons.Count; i++)
            {
                int slot = i;
                if (_partySlotClearButtons[i] != null)
                    _partySlotClearButtons[i].onClick.AddListener(() => RemoveFromSlot(slot));
            }
        }

        private void OnEnable()
        {
            LoadParty();
            Refresh();
        }

        // ---- Party ----------------------------------------------------------

        // The saved party, minus anything that no longer exists or isn't unlocked.
        // The tavern panel is usually hidden, so TownFlow may ask for the
        // party before the panel has ever been shown.
        private void EnsureLoaded()
        {
            if (!_loaded) LoadParty();
        }

        private void LoadParty()
        {
            _loaded = true;
            _party.Clear();
            if (_registry == null) return;

            foreach (var id in ProfileService.Data.party)
            {
                var type = _registry.Find(id);
                if (type != null && ProfileService.IsUnlocked(type) && _party.Count < PartySize
                    && (_allowDuplicateTypes || !_party.Contains(type)))
                    _party.Add(type);
            }
        }

        private void SaveParty()
        {
            ProfileService.Data.party.Clear();
            foreach (var type in _party) ProfileService.Data.party.Add(type.Id);
            ProfileService.Save();
            PartyChanged?.Invoke();
        }

        private void AddToParty(HeroTypeSO type)
        {
            if (_party.Count >= PartySize) { Say("Your party is full. Remove someone first."); return; }
            if (!_allowDuplicateTypes && _party.Contains(type)) return;

            _party.Add(type);
            SaveParty();
            Say(string.Empty);
            Refresh();
        }

        private void RemoveFromParty(HeroTypeSO type)
        {
            if (!_party.Remove(type)) return;
            SaveParty();
            Say(string.Empty);
            Refresh();
        }

        private void RemoveFromSlot(int slot)
        {
            if (slot < 0 || slot >= _party.Count) return;
            _party.RemoveAt(slot);
            SaveParty();
            Say(string.Empty);
            Refresh();
        }

        private void Recruit(HeroTypeSO type)
        {
            if (ProfileService.TryRecruit(type)) Say($"{type.DisplayName} recruited.");
            else Say($"Not enough gems to recruit {type.DisplayName}.");
            Refresh();
        }

        // ---- Display --------------------------------------------------------

        public void Refresh()
        {
            if (_gemsLabel != null) _gemsLabel.text = $"Gems: {ProfileService.Gems}";

            RebuildRows();
            RefreshPartySlots();
        }

        private void RebuildRows()
        {
            if (_rowContainer == null || _rowPrefab == null || _registry == null) return;

            for (int i = _rowContainer.childCount - 1; i >= 0; i--)
                Destroy(_rowContainer.GetChild(i).gameObject);

            foreach (var type in _registry.Types)
            {
                if (type == null) continue;
                var row = Instantiate(_rowPrefab, _rowContainer);
                var captured = type;

                if (!ProfileService.IsUnlocked(type))
                {
                    row.Bind(type, $"Recruit ({type.GemCost} gems)", ProfileService.Gems >= type.GemCost, () => Recruit(captured));
                }
                else if (!_allowDuplicateTypes && _party.Contains(type))
                {
                    row.Bind(type, "Remove", true, () => RemoveFromParty(captured));
                }
                else
                {
                    row.Bind(type, "Add to party", _party.Count < PartySize, () => AddToParty(captured));
                }
            }
        }

        private void RefreshPartySlots()
        {
            for (int i = 0; i < _partySlotLabels.Count; i++)
            {
                if (_partySlotLabels[i] != null)
                    _partySlotLabels[i].text = i < _party.Count ? _party[i].DisplayName : "(empty)";
                if (i < _partySlotClearButtons.Count && _partySlotClearButtons[i] != null)
                    _partySlotClearButtons[i].gameObject.SetActive(i < _party.Count);
            }
        }

        private void Say(string text)
        {
            if (_messageLabel != null) _messageLabel.text = text;
        }
    }
}
