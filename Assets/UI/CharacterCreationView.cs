using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Core;
using DungeonTower.Items;

namespace DungeonTower.UI
{
    /// <summary>
    /// The character creation screen: three heroes (switch with the tabs),
    /// each with a name, a few points to spend on Body/Mind/Spirit on top
    /// of the 3/3/3 start, and starting gear picked from a StarterKitSO
    /// with a point budget — one weapon, one armor, and up to a belt's
    /// worth of potions/scrolls. Weapons and armor that the hero's chosen
    /// stats can't equip are refused, and lowering a stat drops any gear
    /// it no longer qualifies for, so a hero can never start unarmed by
    /// accident of the rules. It never blocks Start; it warns instead
    /// (no weapon, unspent points, names that look alike on the grid).
    ///
    /// Purely a view: it raises StartRequested with the finished
    /// HeroSetups and BackRequested, and TitleFlow decides what happens.
    /// </summary>
    public sealed class CharacterCreationView : MonoBehaviour
    {
        private const int PartySize = 3;
        private const int MaxNameLength = 12;
        private static readonly string[] DefaultNames = { "Alex", "Blake", "Casey" };

        [Serializable]
        public sealed class HeroTab
        {
            public Button Button;
            public TextMeshProUGUI Label;
        }

        [Serializable]
        public sealed class StatRow
        {
            public PrimaryStat Stat;
            [Tooltip("Shows '<Stat> <value>'.")]
            public TextMeshProUGUI Label;
            public Button MinusButton;
            public Button PlusButton;
        }

        [SerializeField] private StarterKitSO _kit;

        [Header("Hero tabs (one per hero, in party order)")]
        [SerializeField] private List<HeroTab> _tabs = new List<HeroTab>();

        [Header("Name and stats")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TextMeshProUGUI _pointsLabel;
        [SerializeField] private List<StatRow> _statRows = new List<StatRow>();
        [Tooltip("Optional. HP, MP, attack, etc. from the chosen stats (before gear).")]
        [SerializeField] private TextMeshProUGUI _previewLabel;

        [Header("Gear")]
        [SerializeField] private TextMeshProUGUI _budgetLabel;
        [SerializeField] private TMP_Dropdown _weaponDropdown;
        [SerializeField] private TMP_Dropdown _armorDropdown;
        [Tooltip("One dropdown per belt slot to fill (potions and scrolls).")]
        [SerializeField] private List<TMP_Dropdown> _beltDropdowns = new List<TMP_Dropdown>();

        [Header("Feedback and navigation")]
        [Tooltip("Why the last click was refused, e.g. 'Iron Sword needs Body 5.'")]
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [Tooltip("Standing heads-ups (no weapon, unspent points, look-alike names). Never blocks Start.")]
        [SerializeField] private TextMeshProUGUI _warningLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _backButton;

        public event Action<IReadOnlyList<HeroSetup>> StartRequested;
        public event Action BackRequested;

        // One pickable item in a dropdown. Gated options (weapon/armor)
        // can't be chosen unless the hero's stats meet the requirement;
        // belt items are never gated here (their requirement is for use).
        private sealed class Option
        {
            public string Name;
            public int Cost;
            public object Item;
            public PrimaryStat Stat;
            public int Required;
            public bool Gated;
        }

        // Everything chosen for one hero so far. Indexes point into the
        // option lists; -1 means "none".
        private sealed class Draft
        {
            public string Name;
            public int Body, Mind, Spirit;   // creation points spent
            public int Weapon = -1;
            public int Armor = -1;
            public readonly int[] Belt;

            public Draft(string name, int beltSlots)
            {
                Name = name;
                Belt = new int[beltSlots];
                for (int i = 0; i < Belt.Length; i++) Belt[i] = -1;
            }

            public int PointsLeft => LevelUpRules.StartingPoints - (Body + Mind + Spirit);

            public int Added(PrimaryStat stat)
            {
                switch (stat)
                {
                    case PrimaryStat.Body: return Body;
                    case PrimaryStat.Mind: return Mind;
                    default: return Spirit;
                }
            }

            public void Add(PrimaryStat stat, int delta)
            {
                switch (stat)
                {
                    case PrimaryStat.Body: Body += delta; break;
                    case PrimaryStat.Mind: Mind += delta; break;
                    default: Spirit += delta; break;
                }
            }

            public int Value(PrimaryStat stat) => LevelUpRules.StartingStatValue + Added(stat);

            public StatBlock Stats => new StatBlock(Value(PrimaryStat.Body), Value(PrimaryStat.Mind), Value(PrimaryStat.Spirit));
        }

        private readonly List<Option> _weaponOptions = new List<Option>();
        private readonly List<Option> _armorOptions = new List<Option>();
        private readonly List<Option> _beltOptions = new List<Option>();
        private readonly List<Draft> _drafts = new List<Draft>();
        private int _selected;

        // ---- Setup ---------------------------------------------------------------

        private void Awake()
        {
            BuildOptions();

            for (int i = 0; i < PartySize; i++)
                _drafts.Add(new Draft(DefaultNames[i % DefaultNames.Length], _beltDropdowns.Count));

            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                if (_tabs[i].Button != null) _tabs[i].Button.onClick.AddListener(() => SelectHero(index));
            }

            if (_nameInput != null)
            {
                _nameInput.characterLimit = MaxNameLength;
                _nameInput.onValueChanged.AddListener(OnNameChanged);
            }

            foreach (var row in _statRows)
            {
                var captured = row;
                if (captured.MinusButton != null) captured.MinusButton.onClick.AddListener(() => AdjustStat(captured.Stat, -1));
                if (captured.PlusButton != null) captured.PlusButton.onClick.AddListener(() => AdjustStat(captured.Stat, +1));
            }

            if (_weaponDropdown != null)
                _weaponDropdown.onValueChanged.AddListener(v => PickGear(_weaponOptions, d => d.Weapon, (d, i) => d.Weapon = i, v - 1));
            if (_armorDropdown != null)
                _armorDropdown.onValueChanged.AddListener(v => PickGear(_armorOptions, d => d.Armor, (d, i) => d.Armor = i, v - 1));
            for (int i = 0; i < _beltDropdowns.Count; i++)
            {
                int slot = i;
                if (_beltDropdowns[i] != null)
                    _beltDropdowns[i].onValueChanged.AddListener(v =>
                        PickGear(_beltOptions, d => d.Belt[slot], (d, idx) => d.Belt[slot] = idx, v - 1));
            }

            if (_startButton != null) _startButton.onClick.AddListener(() => StartRequested?.Invoke(BuildSetups()));
            if (_backButton != null) _backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        private void BuildOptions()
        {
            if (_kit == null)
            {
                Debug.LogError("CharacterCreationView has no StarterKitSO assigned — there will be no gear to pick.", this);
                return;
            }

            foreach (var e in _kit.Weapons)
                if (e != null && e.Item != null)
                    _weaponOptions.Add(new Option { Name = e.Item.Name, Cost = e.Cost, Item = e.Item,
                        Stat = e.Item.RequiredStat, Required = e.Item.RequiredStatValue, Gated = true });

            foreach (var e in _kit.Armors)
                if (e != null && e.Item != null)
                    _armorOptions.Add(new Option { Name = e.Item.Name, Cost = e.Cost, Item = e.Item,
                        Stat = e.Item.RequiredStat, Required = e.Item.RequiredStatValue, Gated = true });

            foreach (var e in _kit.Potions)
                if (e != null && e.Item != null)
                    _beltOptions.Add(new Option { Name = e.Item.Name, Cost = e.Cost, Item = e.Item,
                        Stat = e.Item.RequiredStat, Required = e.Item.RequiredStatValue, Gated = false });

            foreach (var e in _kit.Scrolls)
                if (e != null && e.Item != null)
                    _beltOptions.Add(new Option { Name = e.Item.Name, Cost = e.Cost, Item = e.Item,
                        Stat = e.Item.RequiredStat, Required = e.Item.RequiredStatValue, Gated = false });
        }

        // ---- Public API ----------------------------------------------------------

        // Shows the screen on the first hero. Safe to call while inactive —
        // activating runs Awake first. Choices already made are kept, so
        // backing out to the title and returning loses nothing.
        public void Open()
        {
            gameObject.SetActive(true);
            _selected = 0;
            Say(null);
            RefreshAll();
        }

        public void Close() => gameObject.SetActive(false);

        // ---- Input handlers ------------------------------------------------------

        private Draft Current => _drafts[_selected];

        private void SelectHero(int index)
        {
            if (index < 0 || index >= _drafts.Count) return;
            _selected = index;
            Say(null);
            RefreshAll();
        }

        private void OnNameChanged(string text)
        {
            Current.Name = text;
            RefreshTabs();
            RefreshWarnings();
        }

        private void AdjustStat(PrimaryStat stat, int delta)
        {
            var d = Current;
            if (delta > 0 && d.PointsLeft <= 0) return;
            if (delta < 0 && d.Added(stat) <= 0) return;

            d.Add(stat, delta);
            Say(null);

            // Lowering a stat can strip gear the hero no longer qualifies for.
            if (d.Weapon >= 0 && IsUnmet(_weaponOptions[d.Weapon], d))
            {
                Say($"{_weaponOptions[d.Weapon].Name} was removed — it needs {_weaponOptions[d.Weapon].Stat} {_weaponOptions[d.Weapon].Required}.");
                d.Weapon = -1;
            }
            if (d.Armor >= 0 && IsUnmet(_armorOptions[d.Armor], d))
            {
                Say($"{_armorOptions[d.Armor].Name} was removed — it needs {_armorOptions[d.Armor].Stat} {_armorOptions[d.Armor].Required}.");
                d.Armor = -1;
            }

            RefreshAll();
        }

        // One handler for every gear dropdown: accept the pick if the hero
        // can equip it and afford it, otherwise refuse with a reason. The
        // refresh at the end snaps a refused dropdown back to its old value.
        private void PickGear(List<Option> options, Func<Draft, int> get, Action<Draft, int> set, int newIndex)
        {
            var d = Current;
            int oldIndex = get(d);

            if (newIndex >= 0)
            {
                var option = options[newIndex];
                int costAfter = GearCost(d) - (oldIndex >= 0 ? options[oldIndex].Cost : 0) + option.Cost;

                if (IsUnmet(option, d))
                {
                    Say($"{option.Name} needs {option.Stat} {option.Required}.");
                    RefreshAll();
                    return;
                }
                if (costAfter > _kit.Budget)
                {
                    Say($"Not enough gear points for {option.Name} ({costAfter - _kit.Budget} over budget).");
                    RefreshAll();
                    return;
                }
            }

            set(d, newIndex);
            Say(null);
            RefreshAll();
        }

        private static bool IsUnmet(Option option, Draft d) => option.Gated && d.Value(option.Stat) < option.Required;

        private int GearCost(Draft d)
        {
            int cost = 0;
            if (d.Weapon >= 0) cost += _weaponOptions[d.Weapon].Cost;
            if (d.Armor >= 0) cost += _armorOptions[d.Armor].Cost;
            foreach (int b in d.Belt) if (b >= 0) cost += _beltOptions[b].Cost;
            return cost;
        }

        // ---- Output ---------------------------------------------------------------

        private List<HeroSetup> BuildSetups()
        {
            var result = new List<HeroSetup>();
            for (int i = 0; i < _drafts.Count; i++)
            {
                var d = _drafts[i];
                var setup = new HeroSetup
                {
                    Name = NameOf(i),
                    Body = d.Body,
                    Mind = d.Mind,
                    Spirit = d.Spirit,
                    Weapon = d.Weapon >= 0 ? (IWeapon)_weaponOptions[d.Weapon].Item : null,
                    Armor = d.Armor >= 0 ? (IArmor)_armorOptions[d.Armor].Item : null
                };

                foreach (int b in d.Belt)
                {
                    if (b < 0) continue;
                    var item = _beltOptions[b].Item;
                    if (item is IPotion potion) setup.BeltPotions.Add(potion);
                    else if (item is IScroll scroll) setup.BeltScrolls.Add(scroll);
                }
                result.Add(setup);
            }
            return result;
        }

        // A blank name falls back to the default so nobody starts unnamed.
        private string NameOf(int index)
        {
            string name = _drafts[index].Name != null ? _drafts[index].Name.Trim() : "";
            return name.Length > 0 ? name : DefaultNames[index % DefaultNames.Length];
        }

        // ---- Rendering --------------------------------------------------------------

        private void RefreshAll()
        {
            var d = Current;

            RefreshTabs();

            if (_nameInput != null && _nameInput.text != d.Name) _nameInput.SetTextWithoutNotify(d.Name);

            foreach (var row in _statRows)
            {
                if (row.Label != null) row.Label.text = $"{row.Stat} {d.Value(row.Stat)}";
                if (row.PlusButton != null) row.PlusButton.interactable = d.PointsLeft > 0;
                if (row.MinusButton != null) row.MinusButton.interactable = d.Added(row.Stat) > 0;
            }

            if (_pointsLabel != null)
                _pointsLabel.text = d.PointsLeft > 0
                    ? UiText.Highlight($"Points to spend: {d.PointsLeft}")
                    : "Points to spend: 0";

            if (_previewLabel != null)
            {
                var s = new UnitStats(d.Stats);
                _previewLabel.text =
                    $"HP {s.MaxHp}   MP {s.MaxMp}\n" +
                    $"Physical Attack {UiText.Num(s.PhysicalAttack)}   Defense {UiText.Num(s.PhysicalDefense)}\n" +
                    $"Magic Attack {UiText.Num(s.MagicAttack)}   Defense {UiText.Num(s.MagicDefense)}\n" +
                    $"Initiative {s.Initiative}   Move {s.MoveRange}";
            }

            FillDropdown(_weaponDropdown, _weaponOptions, d, d.Weapon, "No weapon");
            FillDropdown(_armorDropdown, _armorOptions, d, d.Armor, "No armor");
            for (int i = 0; i < _beltDropdowns.Count; i++)
                FillDropdown(_beltDropdowns[i], _beltOptions, d, d.Belt[i], "Empty belt slot");

            int budget = _kit != null ? _kit.Budget : 0;
            if (_budgetLabel != null) _budgetLabel.text = $"Gear points: {GearCost(d)} / {budget}";

            RefreshWarnings();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool exists = i < _drafts.Count;
                if (tab.Button != null) tab.Button.gameObject.SetActive(exists);
                if (!exists || tab.Label == null) continue;

                tab.Label.text = NameOf(i);
                tab.Label.fontStyle = i == _selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // Rebuilt on every refresh because the "needs Body 5" notes depend
        // on the hero's current stats. SetValueWithoutNotify so refilling
        // never fires the pick handler.
        private void FillDropdown(TMP_Dropdown dropdown, List<Option> options, Draft d, int selected, string noneLabel)
        {
            if (dropdown == null) return;

            var labels = new List<string> { noneLabel };
            foreach (var option in options) labels.Add(LabelFor(option, d));

            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            dropdown.SetValueWithoutNotify(selected + 1);
            dropdown.RefreshShownValue();
        }

        private static string LabelFor(Option option, Draft d)
        {
            string text = option.Cost > 0 ? $"{option.Name}  ({option.Cost} pt)" : option.Name;

            if (option.Required > 0)
            {
                if (option.Gated)
                    text += IsUnmet(option, d)
                        ? "  " + UiText.Unmet($"needs {option.Stat} {option.Required}")
                        : $"  (needs {option.Stat} {option.Required})";
                else
                    text += $"  (use: {option.Stat} {option.Required})";
            }
            return text;
        }

        private void RefreshWarnings()
        {
            if (_warningLabel == null) return;

            var lines = new List<string>();
            for (int i = 0; i < _drafts.Count; i++)
            {
                var d = _drafts[i];
                if (d.Weapon < 0) lines.Add($"{NameOf(i)} has no weapon");
                if (d.PointsLeft > 0)
                    lines.Add($"{NameOf(i)} has {d.PointsLeft} unspent point{(d.PointsLeft == 1 ? "" : "s")} (kept for the character sheet)");
            }

            // Units show only their first letter on the grid for now.
            var byLetter = new Dictionary<char, List<string>>();
            for (int i = 0; i < _drafts.Count; i++)
            {
                string name = NameOf(i);
                char letter = char.ToUpperInvariant(name[0]);
                if (!byLetter.TryGetValue(letter, out var names)) byLetter[letter] = names = new List<string>();
                names.Add(name);
            }
            foreach (var pair in byLetter)
                if (pair.Value.Count > 1)
                    lines.Add($"{string.Join(" and ", pair.Value)} both show as '{pair.Key}' on the grid");

            _warningLabel.text = lines.Count == 0 ? "" : UiText.Highlight(string.Join("\n", lines));
        }

        private void Say(string message)
        {
            if (_messageLabel != null) _messageLabel.text = message ?? "";
        }
    }
}
