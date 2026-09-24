using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using DungeonTower.Core;
using DungeonTower.Combat;
using DungeonTower.Generation;
using DungeonTower.Items;

namespace DungeonTower.UI
{
    /// <summary>
    /// First-playable battle orchestrator: builds a map and roster, runs
    /// Battle's turn loop, and handles player clicks.
    ///
    /// A player's turn is always in exactly one mode: Move, Ability (a
    /// weapon ability, 0 or 1), or Scroll (an aimed offensive scroll).
    /// Selecting Move or an ability via the ability bar switches mode
    /// immediately — no separate toggle needed. Right-click or Escape
    /// cancels Ability/Scroll mode back to Move. While aiming an area
    /// ability (Blast/Line/Cone), hovering the grid live-previews the
    /// footprint; a click on a valid tile resolves the attack (single
    /// target or AoE) and ends the turn. Attacks (player or enemy) also
    /// require line of sight, not just range. A "find a firing position
    /// with range + LOS, else close the real-path distance" rule controls
    /// enemy movement until real AI gets built as its own piece. Dead
    /// units drop their gear as loot, picked up automatically by walking
    /// onto the tile; a tinted overlay shows every un-alerted enemy's
    /// detection radius (LOS-clipped) so the player can see where it's
    /// safe to approach.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private UnitView _unitViewPrefab;
        [SerializeField] private AbilityBarView _abilityBar;
        [SerializeField] private InventoryPanelView _inventoryPanel;
        [SerializeField] private GameObject _inventoryToggleButton;
        [SerializeField] private ThemeRegistry _themeRegistry;
        [SerializeField] private GameObject _lootMarkerPrefab;
        [SerializeField] private GameObject _dangerZoneMarkerPrefab;
        [SerializeField] private LootWindowView _lootWindow;
        [SerializeField] private TurnTrackerView _turnTracker;
        [SerializeField] private GameObject _lootToggleButton;
        [SerializeField] private HealthBarView _healthBarPrefab;
        [SerializeField] private RectTransform _healthBarContainer;
        [SerializeField] private List<Chest> _chests = new List<Chest>();

        // The player's starting kit — direct asset references, same
        // reasoning as EnemySO: no ID to keep in sync, just drag the
        // weapon/armor you want in here.
        [SerializeField] private WeaponSO _warriorWeapon;
        [SerializeField] private ArmorSO _warriorArmor;
        [SerializeField] private WeaponSO _adeptWeapon;
        [SerializeField] private ArmorSO _adeptArmor;

        // Spare starting inventory (unequipped) — lists rather than
        // fixed fields since this is naturally variable-length; to
        // start with 2 Health Potions, just drag that asset into the
        // list twice.
        [SerializeField] private List<WeaponSO> _startingSpareWeapons = new List<WeaponSO>();
        [SerializeField] private List<ArmorSO> _startingSpareArmor = new List<ArmorSO>();
        [SerializeField] private List<PotionSO> _startingPotions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _startingScrolls = new List<ScrollSO>();

        // PLACEHOLDER pending a real belt-loading screen: what each
        // hero's belt starts the game with, authored directly rather
        // than assigned in-editor. Each list entry fills one belt slot
        // in order (repeat an asset to give it more than one slot's
        // worth — see LoadBeltFromLists); this is independent of the
        // shared-stash lists above, not drawn from them, purely for
        // testing until belt-loading exists.
        [SerializeField] private List<PotionSO> _warriorStartingPotions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _warriorStartingScrolls = new List<ScrollSO>();
        [SerializeField] private List<PotionSO> _adeptStartingPotions = new List<PotionSO>();
        [SerializeField] private List<ScrollSO> _adeptStartingScrolls = new List<ScrollSO>();

        [SerializeField] private int _mapWidth = 24;
        [SerializeField] private int _mapHeight = 16;

        // Hook for future floor progression — always 1 until a
        // "descend to the next floor" flow exists to increment it.
        // Both the theme lookup and the room/corridor size formula
        // already key off this, so wiring that flow in later is just a
        // matter of setting this field and rebuilding the roster.
        [SerializeField] private int _currentFloor = 1;
        private int _bossesSpawnedThisFloor;

        private enum ActionMode { Move, Ability, Scroll }

        private readonly System.Random _rng = new System.Random();
        private DungeonMap _map;
        private Battle _battle;
        private readonly Dictionary<CombatUnit, UnitView> _unitViews = new Dictionary<CombatUnit, UnitView>();
        private HashSet<GridPosition> _reachableTiles = new HashSet<GridPosition>();
        private ActionMode _mode = ActionMode.Move;
        private int _selectedAbilityIndex;
        private IScroll _pendingScroll;
        private readonly PartyInventory _inventory = new PartyInventory();
        private readonly List<LootDrop> _lootOnGround = new List<LootDrop>();
        private readonly Dictionary<GridPosition, GameObject> _lootMarkers = new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, GameObject> _dangerZoneMarkers = new Dictionary<GridPosition, GameObject>();
        private List<CombatUnit> _partyMembers;
        public IReadOnlyList<CombatUnit> PartyMembers => _partyMembers;

        private void Awake()
        {
            _abilityBar.AbilitySelected += OnAbilitySelected;
            _abilityBar.MoveSelected += OnMoveSelected;
            _inventoryPanel.WeaponEquipRequested += OnWeaponEquipRequested;
            _inventoryPanel.ArmorEquipRequested += OnArmorEquipRequested;
            _inventoryPanel.PotionUseRequested += OnPotionUseRequested;
            _inventoryPanel.ScrollUseRequested += OnScrollUseRequested;
            _lootWindow.WeaponTakeRequested += OnLootWeaponTakeRequested;
            _lootWindow.ArmorTakeRequested += OnLootArmorTakeRequested;
            _lootWindow.PotionTakeRequested += OnLootPotionTakeRequested;
            _lootWindow.ScrollTakeRequested += OnLootScrollTakeRequested;
            _lootWindow.TakeAllRequested += OnTakeAllLootRequested;
            _lootWindow.CloseRequested += OnLootWindowCloseRequested;
        }

        private void OnDestroy()
        {
            if (_abilityBar != null)
            {
                _abilityBar.AbilitySelected -= OnAbilitySelected;
                _abilityBar.MoveSelected -= OnMoveSelected;
            }
            if (_inventoryPanel != null)
            {
                _inventoryPanel.WeaponEquipRequested -= OnWeaponEquipRequested;
                _inventoryPanel.ArmorEquipRequested -= OnArmorEquipRequested;
                _inventoryPanel.PotionUseRequested -= OnPotionUseRequested;
                _inventoryPanel.ScrollUseRequested -= OnScrollUseRequested;
            }
            if (_lootWindow != null)
            {
                _lootWindow.WeaponTakeRequested -= OnLootWeaponTakeRequested;
                _lootWindow.ArmorTakeRequested -= OnLootArmorTakeRequested;
                _lootWindow.PotionTakeRequested -= OnLootPotionTakeRequested;
                _lootWindow.ScrollTakeRequested -= OnLootScrollTakeRequested;
                _lootWindow.TakeAllRequested -= OnTakeAllLootRequested;
                _lootWindow.CloseRequested -= OnLootWindowCloseRequested;
            }
        }

        // Forwards to the panel's own Toggle() — lets the Inventory
        // button's OnClick target BattleController directly. Opening the
        // panel backs out of any in-progress aim, so a stray click on a
        // grid tile behind the panel can't be misread as a target.
        public void ToggleInventoryPanel()
        {
            if (_inventoryPanel == null) return;
            if (!_inventoryPanel.gameObject.activeSelf)
            {
                if (_mode != ActionMode.Move) SetMode(ActionMode.Move);
                if (_lootWindow != null) _lootWindow.Hide();
            }
            _inventoryPanel.Toggle();
        }

        // Same shape as ToggleInventoryPanel — lets a Loot button appear
        // whenever the current unit is standing on/adjacent to something
        // lootable (see RefreshLootAvailability) and open a window
        // listing every in-range container's contents as click-to-take
        // rows.
        public void ToggleLootWindow()
        {
            if (_lootWindow == null || _battle == null) return;

            if (_lootWindow.gameObject.activeSelf)
            {
                _lootWindow.Hide();
                return;
            }

            if (_mode != ActionMode.Move) SetMode(ActionMode.Move);
            _inventoryPanel.Hide();
            _lootWindow.Show(GetLootableContainersInRange(_battle.CurrentUnit));
        }

        private void Start()
        {
            var dungeon = MapGenerator.GenerateProcedural(_mapWidth, _mapHeight);
            _map = dungeon.Map;
            Debug.Log($"Generated {dungeon.Rooms.Count} room(s)");
            _gridView.BuildFrom(_map);

            SeedStartingInventory();
            _inventoryPanel.Initialize(_inventory);
            _inventoryPanel.Hide();

            var units = BuildStartingRoster(dungeon);
            _partyMembers = units.Where(u => u.Faction == Faction.Player).ToList();
            _inventoryPanel.ConfigurePartyNames(_partyMembers[0].DisplayName, _partyMembers[1].DisplayName);
            _inventoryPanel.SetPartyMembers(_partyMembers);

            foreach (var unit in units)
                SpawnUnitView(unit);

            _battle = new Battle(units);
            _battle.Start();
            BeginTurn();
        }

        // Placeholder stash so there's something to test the equip/use
        // flow with, before real loot drops exist. Deliberately leaves
        // out Sword/Plate since those are already equipped at the start.
        private void SeedStartingInventory()
        {
            foreach (var weapon in _startingSpareWeapons) _inventory.AddWeapon(weapon);
            foreach (var armor in _startingSpareArmor) _inventory.AddArmor(armor);
            foreach (var potion in _startingPotions) _inventory.AddPotion(potion);
            foreach (var scroll in _startingScrolls) _inventory.AddScroll(scroll);
        }

        // PLACEHOLDER for a real belt-loading screen: packs each list
        // into consecutive belt slots, grouping repeated entries into
        // one slot's stack count (so listing "Health Potion" twice
        // fills one slot with a count of 2, rather than using two
        // slots). Potions fill slots first, then scrolls; anything past
        // the belt's slot count is silently dropped.
        private static void LoadBeltFromLists(Belt belt, List<PotionSO> potions, List<ScrollSO> scrolls)
        {
            int slot = 0;
            foreach (var group in potions.GroupBy(p => p))
            {
                if (slot >= belt.SlotCount) return;
                belt.SetSlot(slot++, group.Key, group.Count());
            }
            foreach (var group in scrolls.GroupBy(s => s))
            {
                if (slot >= belt.SlotCount) return;
                belt.SetSlot(slot++, group.Key, group.Count());
            }
        }

        private const int CorridorRoamRadius = 6;

        // Slot-count tuning: how many tiles of room/corridor area "buy"
        // one enemy slot, clamped to a sane range, plus a small bump per
        // few floors. All placeholders, same as everything else that's
        // still in first-pass tuning.
        private const int TilesPerEnemyRoom = 6;
        private const int MinPerRoom = 1;
        private const int MaxPerRoom = 5;
        private const int TilesPerEnemyCorridor = 8;
        private const int MinPerCorridor = 0;
        private const int MaxPerCorridor = 4;
        private const int FloorsPerSizeBonus = 3;
        private const int MaxRolePickAttempts = 5;

        private List<CombatUnit> BuildStartingRoster(GeneratedDungeon dungeon)
        {
            var playerSpawns = SpawnZones.PlayerSpawns(dungeon, 2);

            var warrior = new CombatUnit("Warrior", Faction.Player,
                new UnitStats(ClassLibrary.Get(ClassId.Warrior)), playerSpawns[0]);
            warrior.TryEquip(_warriorWeapon);
            warrior.Stats.TryEquipArmor(_warriorArmor);
            LoadBeltFromLists(warrior.Belt, _warriorStartingPotions, _warriorStartingScrolls);

            var adept = new CombatUnit("Adept", Faction.Player,
                new UnitStats(ClassLibrary.Get(ClassId.Adept)), playerSpawns[1]);
            adept.TryEquip(_adeptWeapon);
            adept.Stats.TryEquipArmor(_adeptArmor);
            LoadBeltFromLists(adept.Belt, _adeptStartingPotions, _adeptStartingScrolls);

            var units = new List<CombatUnit> { warrior, adept };
            units.AddRange(BuildEnemyRoster(dungeon, playerSpawns));

            return units;
        }

        // Every room except the player's starting room (dungeon.Rooms
        // First()) gets its own encounter, sized by that room's area
        // (plus a small floor bonus) and filled slot-by-slot via
        // weighted role, then weighted-enemy, picks from the floor's
        // active theme. One corridor-patrol group follows the same
        // mechanism at a central anchor point, sized by its roam
        // patch's tile count instead of a room's area. The boss cap is
        // enforced across this whole call, not per room/corridor.
        private List<CombatUnit> BuildEnemyRoster(GeneratedDungeon dungeon, List<GridPosition> playerSpawns)
        {
            var units = new List<CombatUnit>();

            var theme = _themeRegistry != null ? _themeRegistry.GetThemeForFloor(_currentFloor) : null;
            if (theme == null || theme.Enemies.Count == 0)
            {
                Debug.LogError($"BattleController: no usable DungeonThemeSO for floor {_currentFloor} — no enemies will spawn.");
                return units;
            }

            _bossesSpawnedThisFloor = 0;
            var exclude = new List<GridPosition>(playerSpawns);

            foreach (var room in dungeon.Rooms.Skip(1))
            {
                var group = BuildRoomEncounter(dungeon, theme, room, exclude);
                units.AddRange(group);
                exclude.AddRange(group.Select(u => u.Position));
            }

            var anchor = SpawnZones.PickCorridorAnchor(dungeon);
            if (anchor != null)
                units.AddRange(BuildCorridorEncounter(dungeon, theme, anchor.Value, exclude));

            return units;
        }

        private List<CombatUnit> BuildRoomEncounter(
            GeneratedDungeon dungeon, DungeonThemeSO theme, Room room, List<GridPosition> exclude)
        {
            int slotCount = ComputeSlotCount(room.Width * room.Height, TilesPerEnemyRoom, MinPerRoom, MaxPerRoom);
            var spawns = SpawnZones.SpawnsNearPosition(dungeon, room.Center, slotCount, exclude);
            var roamZone = room.Tiles().ToList();
            return SpawnGroup(theme, spawns, roamZone);
        }

        private List<CombatUnit> BuildCorridorEncounter(
            GeneratedDungeon dungeon, DungeonThemeSO theme, GridPosition anchor, List<GridPosition> exclude)
        {
            var roamZone = dungeon.CorridorTilesNear(anchor, CorridorRoamRadius);
            int slotCount = ComputeSlotCount(roamZone.Count, TilesPerEnemyCorridor, MinPerCorridor, MaxPerCorridor);
            if (slotCount <= 0) return new List<CombatUnit>();

            var spawns = SpawnZones.SpawnsNearPosition(dungeon, anchor, slotCount, exclude);
            return SpawnGroup(theme, spawns, roamZone);
        }

        private List<CombatUnit> SpawnGroup(
            DungeonThemeSO theme, List<GridPosition> spawns, IEnumerable<GridPosition> roamZone)
        {
            var group = new List<CombatUnit>();
            foreach (var position in spawns)
            {
                var unit = SpawnEnemyFromTheme(theme, position);
                if (unit == null) continue;
                unit.SetRoamZone(roamZone);
                group.Add(unit);
            }
            return group;
        }

        private int ComputeSlotCount(int areaTiles, int tilesPerEnemy, int min, int max)
        {
            int baseCount = Mathf.Max(1, areaTiles / tilesPerEnemy);
            int floorBonus = (_currentFloor - 1) / FloorsPerSizeBonus;
            return Mathf.Clamp(baseCount + floorBonus, min, max);
        }

        private CombatUnit SpawnEnemyFromTheme(DungeonThemeSO theme, GridPosition position)
        {
            var enemySO = PickEnemy(theme);
            if (enemySO == null) return null;

            if (enemySO.Role == EnemyRole.Boss) _bossesSpawnedThisFloor++;

            var unit = new CombatUnit(enemySO.DisplayName, Faction.Enemy,
                new UnitStats(ClassLibrary.Get(enemySO.ClassId)), position,
                enemySO.DetectionRadius, enemySO.AlertRadius);
            unit.TryEquip(enemySO.Weapon);
            unit.Stats.TryEquipArmor(enemySO.Armor);
            return unit;
        }

        // Two-layer weighted pick: first which ROLE fills this slot
        // (theme.RoleWeights — the "minion way more likely than boss"
        // table), then which specific EnemySO within that role
        // (each entry's own Weight). Rerolls the role if it picks Boss
        // past the theme's cap, or if the theme has no enemy of that
        // role at all. Falls back to any non-Boss enemy if every
        // reroll comes up empty, so a slot never just goes unfilled.
        private EnemySO PickEnemy(DungeonThemeSO theme)
        {
            for (int attempt = 0; attempt < MaxRolePickAttempts; attempt++)
            {
                var role = PickRole(theme);
                if (role == EnemyRole.Boss && _bossesSpawnedThisFloor >= theme.MaxBossesPerFloor)
                    continue;

                var candidates = theme.Enemies
                    .Where(e => e.Enemy != null && e.Enemy.Role == role)
                    .Select(e => (e.Enemy, e.Weight))
                    .ToList();
                if (candidates.Count == 0) continue;

                return WeightedRandom.Pick(candidates, _rng);
            }

            var fallback = theme.Enemies
                .Where(e => e.Enemy != null && e.Enemy.Role != EnemyRole.Boss)
                .Select(e => (e.Enemy, e.Weight))
                .ToList();
            if (fallback.Count > 0) return WeightedRandom.Pick(fallback, _rng);

            return theme.Enemies.Count > 0 ? theme.Enemies[0].Enemy : null; // truly nothing else to offer
        }

        private EnemyRole PickRole(DungeonThemeSO theme)
        {
            if (theme.RoleWeights.Count > 0)
                return WeightedRandom.Pick(
                    theme.RoleWeights.Select(r => (r.Role, r.Weight)).ToList(), _rng);

            // No role weights authored on this theme — fall back to a
            // uniform pick across whichever roles its enemy pool
            // actually has, rather than defaulting to one fixed role.
            var rolesPresent = theme.Enemies
                .Where(e => e.Enemy != null)
                .Select(e => e.Enemy.Role)
                .Distinct()
                .ToList();
            return rolesPresent.Count > 0 ? rolesPresent[_rng.Next(rolesPresent.Count)] : EnemyRole.Minion;
        }

        private void SpawnUnitView(CombatUnit unit)
        {
            var view = Instantiate(_unitViewPrefab, transform);
            char symbol = unit.DisplayName[0];
            view.Bind(unit, symbol);
            _unitViews[unit] = view;
            var bar = Instantiate(_healthBarPrefab, _healthBarContainer);
            bar.Bind(unit);
        }

        private void BeginTurn()
        {
            if (_battle.Outcome != BattleOutcome.InProgress)
            {
                Debug.Log($"Battle over: {_battle.Outcome}");
                _abilityBar.Hide();
                _inventoryPanel.Hide();
                _inventoryToggleButton.SetActive(false);
                if (_lootWindow != null) _lootWindow.Hide();
                if (_lootToggleButton != null) _lootToggleButton.SetActive(false);
                if (_turnTracker != null) _turnTracker.Hide();
                return;
            }

            var unit = _battle.CurrentUnit;
            Debug.Log($"Round {_battle.RoundNumber} — {unit.DisplayName}'s turn ({unit.Faction})");
            unit.TickCooldowns();
            RefreshTurnHighlight();
            if (_turnTracker != null)
                _turnTracker.Refresh(_battle.GetTurnOrder(), _battle.CurrentUnit, IsInCombat());
            RefreshDangerZoneMarkers();
            _gridView.ClearHighlights();
            _pendingScroll = null;
            _inventoryPanel.Hide();
            if (_lootWindow != null) _lootWindow.Hide();

            if (unit.Faction == Faction.Player)
            {
                _inventoryToggleButton.SetActive(true);
                _inventoryPanel.SetTargetMode(IsInCombat(), _partyMembers.IndexOf(unit), unit.Belt);
                RefreshLootAvailability(unit);

                if (unit.EquippedWeapon != null) _abilityBar.Show(unit.EquippedWeapon, unit.GetRemainingCooldown);
                else _abilityBar.ShowMoveOnly();

                SetMode(ActionMode.Move);
            }
            else
            {
                _inventoryToggleButton.SetActive(false);
                if (_lootToggleButton != null) _lootToggleButton.SetActive(false);
                _abilityBar.Hide();
                RunEnemyTurn(unit);
            }
        }

        // Single entry point for switching what the current player's
        // click means. Recomputes highlighting for the new mode and
        // clears any stale AoE preview.
        private void SetMode(ActionMode mode, int abilityIndex = 0)
        {
            _mode = mode;
            _gridView.ClearPreviewOverlay();
            var unit = _battle.CurrentUnit;

            switch (mode)
            {
                case ActionMode.Move:
                    _selectedAbilityIndex = 0;
                    _reachableTiles = MovementRangeCalculator.GetReachableTiles(
                        unit.Position, unit.Stats.MoveRange, _map, OccupiedTilesExcluding(unit));
                    _gridView.SetHighlights(_reachableTiles, Enumerable.Empty<GridPosition>());
                    _abilityBar.SetSelected(-1);
                    break;

                case ActionMode.Ability:
                    _selectedAbilityIndex = abilityIndex;
                    _reachableTiles = new HashSet<GridPosition>();
                    _gridView.SetHighlights(
                        Enumerable.Empty<GridPosition>(),
                        GetValidAbilityTargetTiles(unit, GetSelectedWeaponAbility(unit)));
                    _abilityBar.SetSelected(abilityIndex);
                    break;

                case ActionMode.Scroll:
                    _reachableTiles = new HashSet<GridPosition>();
                    _gridView.SetHighlights(
                        Enumerable.Empty<GridPosition>(),
                        GetValidAbilityTargetTiles(unit, _pendingScroll?.Ability));
                    break;
            }
        }

        private void OnMoveSelected()
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            if (_battle.CurrentUnit.Faction != Faction.Player) return;
            SetMode(ActionMode.Move);
        }

        private void OnAbilitySelected(int index)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = _battle.CurrentUnit;
            if (unit.Faction != Faction.Player || unit.EquippedWeapon == null) return;
            if (index < 0 || index >= unit.EquippedWeapon.Abilities.Count) return;

            var ability = unit.EquippedWeapon.Abilities[index];
            if (unit.IsOnCooldown(ability))
            {
                Debug.Log($"{ability.Name} is on cooldown ({unit.GetRemainingCooldown(ability)} turn(s) left).");
                return;
            }

            SetMode(ActionMode.Ability, index);
        }

        // Equipping/using costs the current unit's turn action, same as
        // moving or attacking — so these only act during a player's own
        // turn, and only a successful change ends it. A failed attempt
        // (stat requirement not met) costs nothing and leaves the panel
        // open.
        private void OnWeaponEquipRequested(IWeapon weapon, int targetIndex)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = ResolveEquipTarget(targetIndex);
            if (unit == null) return;

            if (!unit.TryEquip(weapon, out var previous))
            {
                Debug.Log($"{unit.DisplayName} cannot equip {weapon.Name} — stat requirement not met");
                return;
            }

            _inventory.RemoveWeapon(weapon);
            if (previous != null) _inventory.AddWeapon(previous);
            Debug.Log($"{unit.DisplayName} equips {weapon.Name}" + (previous != null ? $" (was {previous.Name})" : ""));

            ResolveEquipCost();
        }

        private void OnArmorEquipRequested(IArmor armor, int targetIndex)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = ResolveEquipTarget(targetIndex);
            if (unit == null) return;

            if (!unit.Stats.TryEquipArmor(armor, out var previous))
            {
                Debug.Log($"{unit.DisplayName} cannot equip {armor.Name} — stat requirement not met");
                return;
            }

            _inventory.RemoveArmor(armor);
            if (previous != null) _inventory.AddArmor(previous);
            Debug.Log($"{unit.DisplayName} equips {armor.Name}" + (previous != null ? $" (was {previous.Name})" : ""));

            ResolveEquipCost();
        }

        // Potions resolve immediately (self/ally heal, no grid target
        // needed) — same turn-cost rule as equipping.
        // Potions always act on whoever's turn it currently is — there's
        // no target switcher involved, and the item comes out of their
        // own belt, not the shared party stash. Same turn-cost rule as
        // equipping: free out of combat, costs the turn once alerted.
        private void OnPotionUseRequested(IPotion potion)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = _battle.CurrentUnit;
            if (unit.Faction != Faction.Player) return;

            if (!potion.CanUse(unit.Stats.Current))
            {
                Debug.Log($"{unit.DisplayName} cannot use {potion.Name} — stat requirement not met");
                return;
            }
            if (potion.Ability != null && unit.IsOnCooldown(potion.Ability))
            {
                Debug.Log($"{potion.Name} cannot be used yet — {potion.Ability.Name} is on cooldown ({unit.GetRemainingCooldown(potion.Ability)} turn(s) left).");
                return;
            }
            if (!unit.Belt.TryConsume(potion))
            {
                Debug.Log($"{unit.DisplayName}'s belt doesn't have {potion.Name}.");
                return;
            }

            ApplyPotionEffect(unit, potion);
            ResolveEquipCost();
        }

        // Heal/Buff are the meaningful cases for a self-used potion;
        // Damage is accepted (nothing stops authoring it) but doesn't
        // make sense here, so it's just logged and skipped.
        private void ApplyPotionEffect(CombatUnit unit, IPotion potion)
        {
            var ability = potion.Ability;
            if (ability == null) return;

            unit.TriggerCooldown(ability);

            switch (ability.EffectKind)
            {
                case EffectKind.Heal:
                    unit.Heal(ability.HealHp);
                    unit.RestoreMp(ability.HealMp);
                    break;
                case EffectKind.Buff:
                    unit.Stats.AddBonus(ability.Bonus);
                    break;
                default:
                    Debug.LogWarning($"{potion.Name}'s ability is {ability.EffectKind}-kind, which isn't meaningful for a self-used potion — nothing happened.");
                    break;
            }

            _unitViews[unit].Refresh();
            Debug.Log($"{unit.DisplayName} uses {potion.Name}.");
        }

        // Scrolls are different: reading one aims its ability at the
        // grid just like a weapon ability, rather than resolving right
        // away. The scroll is only actually consumed once a valid
        // target tile is clicked (see HandleAbilityClick) — cancelling
        // via Move/right-click/Escape leaves it in the inventory.
        private void OnScrollUseRequested(IScroll scroll)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = _battle.CurrentUnit;
            if (unit.Faction != Faction.Player) return;
            if (scroll.Ability == null) return;
            if (!scroll.CanUse(unit.Stats.Current))
            {
                Debug.Log($"{unit.DisplayName} cannot read {scroll.Name} — stat requirement not met");
                return;
            }
            if (unit.IsOnCooldown(scroll.Ability))
            {
                Debug.Log($"{scroll.Name} cannot be read yet — {scroll.Ability.Name} is on cooldown ({unit.GetRemainingCooldown(scroll.Ability)} turn(s) left).");
                return;
            }

            _pendingScroll = scroll;
            _inventoryPanel.Hide();
            SetMode(ActionMode.Scroll);
        }

        // The switcher only ever offers an index within the party list,
        // and locks to the current unit's index once in combat — so a
        // mis-wired scene (e.g. wrong party size) fails safe instead of
        // throwing. Also refuses a fallen member — nothing should be
        // equippable/usable on (or by) someone who's already dead.
        private CombatUnit ResolveEquipTarget(int partyIndex)
        {
            if (_partyMembers == null || partyIndex < 0 || partyIndex >= _partyMembers.Count) return null;
            var unit = _partyMembers[partyIndex];
            return unit.IsAlive ? unit : null;
        }

        // Out of combat, equipping/using is free — the panel stays open
        // so gear can be mixed and matched without reopening it each
        // time. Once any enemy is alerted, it costs the current unit's
        // turn like any other action, and the panel closes.
        private void ResolveEquipCost()
        {
            if (IsInCombat())
            {
                _inventoryPanel.Hide();
                EndTurn();
            }
            else
            {
                _inventoryPanel.Refresh();
            }
        }

        private bool IsInCombat() => _unitViews.Keys.Any(u => u.Faction == Faction.Enemy && u.IsAlerted);

        // Valid target tiles for the given ability: a single-target
        // ability can only be clicked on a living, LINE-OF-SIGHT-visible
        // enemy within Range; an AoE ability (Blast/Line/Cone) can be
        // aimed at ANY in-bounds tile within Range, occupied or not,
        // since the impact point — not an occupant — is what matters.
        // (AoE targeting doesn't LOS-gate the impact tile itself yet —
        // worth revisiting if you want walls to block area spells too.)
        private List<GridPosition> GetValidAbilityTargetTiles(CombatUnit unit, IAbility ability)
        {
            if (ability == null) return new List<GridPosition>();

            if (ability.AreaShape == AttackShape.Single)
                return GetSingleTargetTiles(unit, ability);

            return AreaOfEffect.GetAffectedTiles(unit.Position, unit.Position, AttackShape.Blast, ability.Range)
                .Where(InBounds)
                .ToList();
        }

        // Damage targets enemies; Heal/Buff target allies (the caster's
        // own tile included, so a single-target heal can be cast on
        // yourself).
        private List<GridPosition> GetSingleTargetTiles(CombatUnit unit, IAbility ability)
        {
            bool targetAllies = ability.EffectKind != EffectKind.Damage;
            return _unitViews.Keys
                .Where(u => u.IsAlive
                    && (targetAllies ? u.Faction == unit.Faction : u.Faction != unit.Faction)
                    && u.Position.ManhattanDistance(unit.Position) <= ability.Range
                    && LineOfSight.HasClearPath(unit.Position, u.Position, _map))
                .Select(u => u.Position)
                .ToList();
        }

        private bool InBounds(GridPosition pos) => pos.X >= 0 && pos.X < _map.Width && pos.Y >= 0 && pos.Y < _map.Height;

        private void RefreshTurnHighlight()
        {
            foreach (var pair in _unitViews)
                pair.Value.SetHighlighted(pair.Key == _battle.CurrentUnit);
        }

        private List<GridPosition> OccupiedTilesExcluding(CombatUnit exclude)
        {
            return _unitViews.Keys
                .Where(u => u.IsAlive && u != exclude)
                .Select(u => u.Position)
                .ToList();
        }

        private void Update()
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            if (_battle.CurrentUnit.Faction != Faction.Player) return;

            HandleCancelInput();
            HandleHoverPreview();

            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return; // click was on a UI element (e.g. an ability button), not the grid

            var clicked = ScreenPointToGridPosition(Mouse.current.position.ReadValue());
            if (clicked != null)
                HandlePlayerClick(clicked.Value);
        }

        // Right-click or Escape backs out of Ability/Scroll aim to Move,
        // without ending the turn or consuming anything. No-op in Move
        // mode already.
        private void HandleCancelInput()
        {
            if (_mode == ActionMode.Move) return;

            bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (rightClick || escape)
            {
                _pendingScroll = null;
                SetMode(ActionMode.Move);
            }
        }

        // Live AoE footprint preview: only relevant while aiming an
        // area-shaped ability at a currently-valid tile.
        private void HandleHoverPreview()
        {
            IAbility ability = _mode switch
            {
                ActionMode.Ability => GetSelectedWeaponAbility(_battle.CurrentUnit),
                ActionMode.Scroll => _pendingScroll?.Ability,
                _ => null
            };

            if (ability == null || ability.AreaShape == AttackShape.Single || Mouse.current == null)
            {
                _gridView.ClearPreviewOverlay();
                return;
            }

            var hovered = ScreenPointToGridPosition(Mouse.current.position.ReadValue());
            if (hovered == null || !GetValidAbilityTargetTiles(_battle.CurrentUnit, ability).Contains(hovered.Value))
            {
                _gridView.ClearPreviewOverlay();
                return;
            }

            var affected = AreaOfEffect.GetAffectedTiles(
                _battle.CurrentUnit.Position, hovered.Value, ability.AreaShape, ability.AreaRadius);
            _gridView.SetPreviewOverlay(affected);
        }

        private GridPosition? ScreenPointToGridPosition(Vector2 screenPoint)
        {
            var worldPoint = Camera.main.ScreenToWorldPoint(screenPoint);
            var hit = Physics2D.OverlapPoint(worldPoint);
            if (hit == null) return null;

            var tile = hit.GetComponent<TileView>();
            return tile != null ? tile.Position : (GridPosition?)null;
        }

        private void HandlePlayerClick(GridPosition target)
        {
            var acting = _battle.CurrentUnit;

            switch (_mode)
            {
                case ActionMode.Move:
                    HandleMoveClick(acting, target);
                    break;
                case ActionMode.Ability:
                    HandleAbilityClick(acting, GetSelectedWeaponAbility(acting), target, isScroll: false);
                    break;
                case ActionMode.Scroll:
                    HandleAbilityClick(acting, _pendingScroll?.Ability, target, isScroll: true);
                    break;
            }
        }

        private void HandleMoveClick(CombatUnit acting, GridPosition target)
        {
            var occupant = FindLivingUnitAt(target);
            if (occupant == null && _reachableTiles.Contains(target))
            {
                acting.Position = target;
                _unitViews[acting].Refresh();
                EndTurn();
            }
        }

        private void HandleAbilityClick(CombatUnit acting, IAbility ability, GridPosition target, bool isScroll)
        {
            if (ability == null) return;
            if (!GetValidAbilityTargetTiles(acting, ability).Contains(target)) return;

            ExecuteAbilityAt(acting, ability, target);
            if (isScroll) acting.Belt.TryConsume(_pendingScroll);
            _pendingScroll = null;

            EndTurn();
        }

        private void RunEnemyTurn(CombatUnit enemy)
        {
            var target = FindNearestPlayer(enemy);
            if (target == null)
            {
                EndTurn();
                return;
            }

            if (!enemy.IsAlerted)
            {
                if (enemy.CanSense(target.Position, _map))
                {
                    AlertUnit(enemy);
                    Debug.Log($"{enemy.DisplayName} spots {target.DisplayName}!");
                }
                else
                {
                    var step = RoamAI.ChooseStep(enemy, _map, OccupiedTilesExcluding(enemy), _rng);
                    if (step.HasValue)
                    {
                        enemy.Position = step.Value;
                        _unitViews[enemy].Refresh();
                    }
                    EndTurn();
                    return;
                }
            }

            var ability = GetEnemyActiveAbility(enemy);
            int range = ability?.Range ?? 1;

            if (enemy.Position.ManhattanDistance(target.Position) <= range
                && LineOfSight.HasClearPath(enemy.Position, target.Position, _map))
            {
                if (ability != null) ExecuteAbilityAt(enemy, ability, target.Position);
                EndTurn();
                return;
            }

            var reachable = MovementRangeCalculator.GetReachableTiles(
                enemy.Position, enemy.Stats.MoveRange, _map, OccupiedTilesExcluding(enemy));
            if (reachable.Count > 0)
            {
                var step = ChooseEnemyStep(enemy, target, range, reachable);
                if (step.HasValue)
                {
                    enemy.Position = step.Value;
                    _unitViews[enemy].Refresh();
                }
            }

            EndTurn();
        }

        // Prefers a reachable tile that already has range AND line of
        // sight to the target — "find a firing position" — breaking ties
        // by real path distance. Falls back to the reachable tile with
        // the smallest real path distance if no such tile exists this
        // turn. Either way, "real path distance" (via PathDistanceField)
        // is what actually fixes walking into dead-end corners — a
        // straight-line-close tile behind a wall now correctly ranks
        // farther than one along an actual route around it.
        private GridPosition? ChooseEnemyStep(
            CombatUnit enemy, CombatUnit target, int range, HashSet<GridPosition> reachable)
        {
            var distanceField = PathDistanceField.BuildFrom(target.Position, _map);

            var firingPosition = reachable
                .Where(pos => pos.ManhattanDistance(target.Position) <= range
                    && LineOfSight.HasClearPath(pos, target.Position, _map))
                .OrderBy(pos => PathDistanceOrMax(distanceField, pos))
                .Select(pos => (GridPosition?)pos)
                .FirstOrDefault();

            if (firingPosition != null) return firingPosition;

            return reachable
                .OrderBy(pos => PathDistanceOrMax(distanceField, pos))
                .Select(pos => (GridPosition?)pos)
                .FirstOrDefault();
        }

        private static int PathDistanceOrMax(Dictionary<GridPosition, int> field, GridPosition pos)
            => field.TryGetValue(pos, out var distance) ? distance : int.MaxValue;

        private CombatUnit FindNearestPlayer(CombatUnit from)
        {
            return _unitViews.Keys
                .Where(u => u.Faction == Faction.Player && u.IsAlive)
                .OrderBy(u => u.Position.ManhattanDistance(from.Position))
                .FirstOrDefault();
        }

        private CombatUnit FindLivingUnitAt(GridPosition position)
        {
            return _unitViews.Keys.FirstOrDefault(u => u.IsAlive && u.Position.Equals(position));
        }

        // Dispatches to every target within the ability's footprint —
        // the one place both HandleAbilityClick and RunEnemyTurn go
        // through, so an enemy wielding an AoE weapon "just works" the
        // same way a player's does. What happens to each target is
        // decided per-target by ApplyAbilityEffect (Damage/Heal/Buff),
        // since that decision doesn't depend on Single vs AoE.
        private void ExecuteAbilityAt(CombatUnit attacker, IAbility ability, GridPosition impactTile)
        {
            attacker.TriggerCooldown(ability);

            if (ability.AreaShape == AttackShape.Single)
            {
                var target = FindLivingUnitAt(impactTile);
                if (target == null) return;
                ApplyAbilityEffect(attacker, target, ability);
                return;
            }

            var affectedTiles = new HashSet<GridPosition>(
                AreaOfEffect.GetAffectedTiles(attacker.Position, impactTile, ability.AreaShape, ability.AreaRadius));

            // Damage AoE excludes the caster (allies can still be caught
            // in it, same as most tactics games); Heal/Buff AoE includes
            // them, since standing in your own heal nova should heal
            // you too.
            bool excludeCaster = ability.EffectKind == EffectKind.Damage;

            foreach (var target in _unitViews.Keys.ToList())
            {
                if (!target.IsAlive) continue;
                if (excludeCaster && target == attacker) continue;
                if (!affectedTiles.Contains(target.Position)) continue;
                ApplyAbilityEffect(attacker, target, ability);
            }
        }

        // Damage goes through the existing crit/defense resolution (and
        // can kill/drop loot, via ResolveAttack); Heal/Buff are simple
        // enough — no RNG, no defense — that they're applied directly
        // here instead of needing their own resolver class.
        private void ApplyAbilityEffect(CombatUnit attacker, CombatUnit target, IAbility ability)
        {
            switch (ability.EffectKind)
            {
                case EffectKind.Damage:
                    ResolveAttack(attacker, target, ability);
                    break;

                case EffectKind.Heal:
                    target.Heal(ability.HealHp);
                    target.RestoreMp(ability.HealMp);
                    _unitViews[target].Refresh();
                    Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {target.DisplayName}, restoring {ability.HealHp} HP / {ability.HealMp} MP.");
                    break;

                case EffectKind.Buff:
                    target.Stats.AddBonus(ability.Bonus);
                    Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {target.DisplayName}, granting a bonus.");
                    break;
            }
        }

        private void ResolveAttack(CombatUnit attacker, CombatUnit defender, IAbility ability)
        {
            var result = AttackResolver.Resolve(attacker, defender, ability, _rng);
            bool wasAlive = defender.IsAlive;
            defender.ApplyDamage(result.Damage);
            AlertUnit(defender);
            _unitViews[defender].Refresh();
            Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {defender.DisplayName} for {result.Damage} {ability.Kind}{(result.IsCrit ? " (CRIT)" : "")}");

            if (wasAlive && !defender.IsAlive)
                HandleDeath(defender);
        }

        // No revive exists — a dead unit is gone for the rest of the run.
        // Whatever it had equipped drops onto its tile as loot, for either
        // side to reclaim.
        private void HandleDeath(CombatUnit unit)
        {
            Debug.Log($"{unit.DisplayName} has fallen.");

            // A natural weapon/armor (claws, thick hide, etc.) never
            // shows up as loot — only real gear does.
            var droppedWeapon = unit.EquippedWeapon != null && unit.EquippedWeapon.DropsOnDeath
                ? unit.EquippedWeapon : null;
            var droppedArmor = unit.Stats.EquippedArmor != null && unit.Stats.EquippedArmor.DropsOnDeath
                ? unit.Stats.EquippedArmor : null;

            if (droppedWeapon != null || droppedArmor != null)
            {
                _lootOnGround.Add(new LootDrop(unit.Position, droppedWeapon, droppedArmor));
                SpawnLootMarker(unit.Position);
            }
        }

        // Every living, un-alerted enemy's detection radius, shown as a
        // tinted overlay — a separate object per tile, not a change to
        // TileView's own color, so it visually blends with whatever
        // move/attack highlight is already on that tile rather than
        // fighting it for the same slot. Matches CanSense exactly,
        // line-of-sight included — a tile behind a wall won't show as
        // dangerous even if it's within raw radius, since it genuinely
        // isn't anymore. Only shows on walkable tiles, since those are
        // the only ones a player could ever actually stand on.
        private void RefreshDangerZoneMarkers()
        {
            var dangerTiles = new HashSet<GridPosition>();
            foreach (var enemy in _unitViews.Keys)
            {
                if (enemy.Faction != Faction.Enemy || !enemy.IsAlive || enemy.IsAlerted) continue;
                foreach (var tile in TilesWithinRadius(enemy.Position, enemy.DetectionRadius))
                    if (_map.IsWalkable(tile) && LineOfSight.HasClearPath(enemy.Position, tile, _map))
                        dangerTiles.Add(tile);
            }

            var stale = _dangerZoneMarkers.Keys.Where(pos => !dangerTiles.Contains(pos)).ToList();
            foreach (var pos in stale)
            {
                Destroy(_dangerZoneMarkers[pos]);
                _dangerZoneMarkers.Remove(pos);
            }

            if (_dangerZoneMarkerPrefab == null) return;
            foreach (var pos in dangerTiles)
            {
                if (_dangerZoneMarkers.ContainsKey(pos)) continue;
                var marker = Instantiate(_dangerZoneMarkerPrefab, transform);
                marker.transform.position = GridToWorld.ToWorldPosition(pos);
                _dangerZoneMarkers[pos] = marker;
            }
        }

        private static IEnumerable<GridPosition> TilesWithinRadius(GridPosition center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    if (System.Math.Abs(dx) + System.Math.Abs(dy) <= radius)
                        yield return new GridPosition(center.X + dx, center.Y + dy);
        }

        private void SpawnLootMarker(GridPosition position)
        {
            if (_lootMarkerPrefab == null) return;
            var marker = Instantiate(_lootMarkerPrefab, transform);
            marker.transform.position = GridToWorld.ToWorldPosition(position);
            _lootMarkers[position] = marker;
        }

        private void RemoveLootMarker(GridPosition position)
        {
            if (_lootMarkers.TryGetValue(position, out var marker))
            {
                Destroy(marker);
                _lootMarkers.Remove(position);
            }
        }

        // Every corpse pile and chest the current unit is close enough
        // to loot right now, filtered to ones that actually still have
        // something in them. A pile needs the unit standing exactly on
        // it (InteractionRadius 0); a chest can be looted from an
        // adjacent tile too (InteractionRadius 1).
        private List<ILootContainer> GetLootableContainersInRange(CombatUnit unit)
        {
            var result = new List<ILootContainer>();

            foreach (var drop in _lootOnGround)
                if (!drop.IsEmpty && unit.Position.ManhattanDistance(drop.Position) <= drop.InteractionRadius)
                    result.Add(drop);

            foreach (var chest in _chests)
                if (chest != null && !chest.IsEmpty
                    && unit.Position.ManhattanDistance(chest.Position) <= chest.InteractionRadius)
                    result.Add(chest);

            return result;
        }

        // Shows/hides the Loot button for whoever's turn it currently
        // is — called once at the start of a player's turn. Doesn't need
        // rechecking mid-turn: moving ends the turn immediately (one
        // action per turn), so the next chance to loot is always at the
        // start of a turn, after BeginTurn has already run this.
        private void RefreshLootAvailability(CombatUnit unit)
        {
            if (_lootToggleButton != null)
                _lootToggleButton.SetActive(GetLootableContainersInRange(unit).Count > 0);
        }

        private void OnLootWeaponTakeRequested(ILootContainer container, IWeapon weapon)
        {
            container.TakeWeapon(weapon);
            _inventory.AddWeapon(weapon);
            Debug.Log($"Looted {weapon.Name}.");
            CleanUpIfEmpty(container);
            ResolveLootCost();
        }

        private void OnLootArmorTakeRequested(ILootContainer container, IArmor armor)
        {
            container.TakeArmor(armor);
            _inventory.AddArmor(armor);
            Debug.Log($"Looted {armor.Name}.");
            CleanUpIfEmpty(container);
            ResolveLootCost();
        }

        private void OnLootPotionTakeRequested(ILootContainer container, IPotion potion)
        {
            container.TakePotion(potion);
            _inventory.AddPotion(potion);
            Debug.Log($"Looted {potion.Name}.");
            CleanUpIfEmpty(container);
            ResolveLootCost();
        }

        private void OnLootScrollTakeRequested(ILootContainer container, IScroll scroll)
        {
            container.TakeScroll(scroll);
            _inventory.AddScroll(scroll);
            Debug.Log($"Looted {scroll.Name}.");
            CleanUpIfEmpty(container);
            ResolveLootCost();
        }

        // Grabs everything from every in-range container in one go.
        // Snapshots each collection with ToList() first since taking
        // mutates the very list/dictionary being iterated.
        private void OnTakeAllLootRequested()
        {
            foreach (var container in GetLootableContainersInRange(_battle.CurrentUnit))
            {
                foreach (var weapon in container.Weapons.ToList())
                {
                    container.TakeWeapon(weapon);
                    _inventory.AddWeapon(weapon);
                }
                foreach (var armor in container.Armors.ToList())
                {
                    container.TakeArmor(armor);
                    _inventory.AddArmor(armor);
                }
                foreach (var pair in container.Potions.ToList())
                    for (int i = 0; i < pair.Value; i++)
                    {
                        container.TakePotion(pair.Key);
                        _inventory.AddPotion(pair.Key);
                    }
                foreach (var pair in container.Scrolls.ToList())
                    for (int i = 0; i < pair.Value; i++)
                    {
                        container.TakeScroll(pair.Key);
                        _inventory.AddScroll(pair.Key);
                    }
                CleanUpIfEmpty(container);
            }

            Debug.Log("Looted everything in range.");
            ResolveLootCost();
        }

        private void OnLootWindowCloseRequested()
        {
            if (_lootWindow != null) _lootWindow.Hide();
        }

        // An emptied corpse pile disappears from the ground entirely
        // (and its marker with it) — an emptied chest just stays put
        // with nothing left to offer, since it's a piece of the scene,
        // not something that was ever going to be removed.
        private void CleanUpIfEmpty(ILootContainer container)
        {
            if (!container.IsEmpty) return;
            if (container is LootDrop drop)
            {
                _lootOnGround.Remove(drop);
                RemoveLootMarker(drop.Position);
            }
        }

        // Matches equip/potion/scroll precedent exactly: free to loot
        // out of combat, but once any enemy is alerted, taking something
        // (including via Take All) costs the current unit's turn like
        // any other action, and the window closes. That means each
        // individual take can end the turn mid-combat, same as equip
        // today — if you'd rather the whole visit be one action (grab
        // several things, pay once on close), this is the spot to change.
        private void ResolveLootCost()
        {
            if (IsInCombat())
            {
                if (_lootWindow != null) _lootWindow.Hide();
                EndTurn();
            }
            else
            {
                var unit = _battle.CurrentUnit;
                RefreshLootAvailability(unit);
                if (_lootWindow != null) _lootWindow.Refresh(GetLootableContainersInRange(unit));
            }
        }

        // Single entry point for alerting a unit — marks it alerted, then
        // wakes any nearby allies within its alert radius (transitively).
        // A no-op if the unit is already alerted, so this is safe to call
        // repeatedly without re-triggering propagation every turn.
        private void AlertUnit(CombatUnit unit)
        {
            if (unit.IsAlerted) return;
            unit.Alert();
            AlertPropagation.PropagateFrom(unit, _unitViews.Keys);
        }

        // Players choose between a weapon's abilities via the ability
        // bar; enemies always use the first ability (see
        // GetEnemyActiveAbility), since they have no UI and don't need
        // one yet.
        private IAbility GetSelectedWeaponAbility(CombatUnit unit)
        {
            var weapon = unit.EquippedWeapon;
            if (weapon == null || weapon.Abilities.Count == 0) return null;
            int index = Mathf.Clamp(_selectedAbilityIndex, 0, weapon.Abilities.Count - 1);
            return weapon.Abilities[index];
        }

        // Tries each of the weapon's abilities in order and uses the
        // first one that isn't on cooldown; null if every one is (the
        // enemy just moves this turn instead of attacking).
        private IAbility GetEnemyActiveAbility(CombatUnit unit)
        {
            var weapon = unit.EquippedWeapon;
            if (weapon == null) return null;

            foreach (var ability in weapon.Abilities)
                if (!unit.IsOnCooldown(ability))
                    return ability;

            return null;
        }

        private void EndTurn()
        {
            _battle.EndCurrentTurn();
            BeginTurn();
        }
    }
}
