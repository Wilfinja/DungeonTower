using DungeonTower.Combat;
using DungeonTower.Core;
using DungeonTower.Generation;
using DungeonTower.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

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
        // Optional. One marker instance per occupied tile of a visible
        // (non-hidden) BattlefieldObject — a totem, wall, or cloud.
        // Nothing is spawned until you assign this; see
        // RefreshBattlefieldViews. Swap for a proper BattlefieldObjectView
        // component later if you want per-object sprites/tinting.
        [SerializeField] private GameObject _battlefieldObjectMarkerPrefab;
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

        // How far a player unit can currently see, for fog of war.
        // Reuses the same DetectionRadius concept enemies already have —
        // see FogOfWar's doc comment.
        [SerializeField] private int _playerVisionRadius = 8;

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
        private BattlefieldObjectRegistry _battlefieldObjects;
        private IWalkableMap _movementMap;
        private IWalkableMap _sightMap;
        private int _lastTickedRound = -1;
        private readonly Dictionary<BattlefieldObject, List<GameObject>> _battlefieldViews = new Dictionary<BattlefieldObject, List<GameObject>>();
        private readonly FogOfWar _fog = new FogOfWar();
        private Battle _battle;
        private readonly Dictionary<CombatUnit, UnitView> _unitViews = new Dictionary<CombatUnit, UnitView>();
        private readonly Dictionary<CombatUnit, HealthBarView> _healthBars = new Dictionary<CombatUnit, HealthBarView>();
        private HashSet<GridPosition> _reachableTiles = new HashSet<GridPosition>();
        private ActionMode _mode = ActionMode.Move;
        private int _selectedAbilityIndex;
        private IScroll _pendingScroll;
        private readonly PartyInventory _inventory = new PartyInventory();
        private readonly List<LootDrop> _lootOnGround = new List<LootDrop>();

        // Which EnemySO each living enemy was spawned from — read once at
        // death for its XP reward and drop table, then removed.
        private readonly Dictionary<CombatUnit, EnemySO> _enemyKits = new Dictionary<CombatUnit, EnemySO>();
        private readonly Dictionary<GridPosition, GameObject> _lootMarkers = new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, GameObject> _dangerZoneMarkers = new Dictionary<GridPosition, GameObject>();
        private List<CombatUnit> _partyMembers;
        public IReadOnlyList<CombatUnit> PartyMembers => _partyMembers;

        private void Awake()
        {
            _abilityBar.AbilitySelected += OnAbilitySelected;
            _abilityBar.MoveSelected += OnMoveSelected;
            _abilityBar.WaitSelected += OnWaitSelected;
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
            _inventoryPanel.MoveRequested += OnInventoryMoveRequested;
        }

        private void OnDestroy()
        {
            if (_abilityBar != null)
            {
                _abilityBar.AbilitySelected -= OnAbilitySelected;
                _abilityBar.MoveSelected -= OnMoveSelected;
                _abilityBar.WaitSelected -= OnWaitSelected;
            }
            if (_inventoryPanel != null)
            {
                _inventoryPanel.WeaponEquipRequested -= OnWeaponEquipRequested;
                _inventoryPanel.ArmorEquipRequested -= OnArmorEquipRequested;
                _inventoryPanel.PotionUseRequested -= OnPotionUseRequested;
                _inventoryPanel.ScrollUseRequested -= OnScrollUseRequested;
                _inventoryPanel.MoveRequested -= OnInventoryMoveRequested;
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
            _battlefieldObjects = new BattlefieldObjectRegistry();
            _movementMap = new MovementMapView(_map, _battlefieldObjects);
            _sightMap = new SightMapView(_map, _battlefieldObjects);
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
            RefreshFogOfWar();
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

        // Interim until the hero creation screen exists: every hero starts
        // identical (LevelUpRules.StartingStats) and spends their creation
        // points here. Runs before TryEquip, so the spent points count
        // toward weapon/armor requirements.
        private static UnitStats NewHeroStats(int body, int mind, int spirit)
        {
            var stats = new UnitStats(LevelUpRules.StartingStats,
                startingPoints: LevelUpRules.StartingPoints);

            for (int i = 0; i < body; i++) stats.TrySpendPoint(PrimaryStat.Body);
            for (int i = 0; i < mind; i++) stats.TrySpendPoint(PrimaryStat.Mind);
            for (int i = 0; i < spirit; i++) stats.TrySpendPoint(PrimaryStat.Spirit);

            return stats;
        }

        private List<CombatUnit> BuildStartingRoster(GeneratedDungeon dungeon)
        {
            var playerSpawns = SpawnZones.PlayerSpawns(dungeon, 2);

            var warrior = new CombatUnit("Warrior", Faction.Player,
                NewHeroStats(body: 3, mind: 0, spirit: 0), playerSpawns[0], _playerVisionRadius);
            warrior.TryEquip(_warriorWeapon);
            warrior.Stats.TryEquipArmor(_warriorArmor);
            LoadBeltFromLists(warrior.Belt, _warriorStartingPotions, _warriorStartingScrolls);

            var adept = new CombatUnit("Adept", Faction.Player,
                NewHeroStats(body: 0, mind: 3, spirit: 0), playerSpawns[1], _playerVisionRadius);
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
    new UnitStats(enemySO.Stats), position,
    enemySO.DetectionRadius, enemySO.AlertRadius,
    enemySO.TargetingStrategy, enemySO.TargetingRange,
    enemySO.SupportTargetingStrategy, enemySO.SupportHealThreshold);
            unit.TryEquip(enemySO.Weapon);
            unit.Stats.TryEquipArmor(enemySO.Armor);
            _enemyKits[unit] = enemySO;
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
            _healthBars[unit] = bar;
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
            if (_battle.RoundNumber != _lastTickedRound)
            {
                _lastTickedRound = _battle.RoundNumber;
                TickBattlefieldObjects();
            }
            unit.TickCooldowns();
            var startTick = unit.Status.OnTurnStart();
            foreach (var line in startTick.Log) Debug.Log(line);
            _unitViews[unit].Refresh();
            if (startTick.OwnerDied)
            {
                HandleDeath(unit);
                EndTurn();
                return;
            }
            if (startTick.SkipTurn)
            {
                Debug.Log($"{unit.DisplayName} is stunned and skips their turn.");
                EndTurn();
                return;
            }
            RefreshTurnHighlight();
            if (_turnTracker != null)
                _turnTracker.Refresh(_battle.GetProjectedTurnOrder(IsTrackable, TurnTrackerMinEntries),
                    _battle.CurrentUnit, IsInCombat());
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

                if (unit.EquippedWeapon != null) _abilityBar.Show(unit.EquippedWeapon, unit.GetRemainingCooldown,
    a => SilenceRules.IsBlocked(unit, a, AbilitySource.Weapon));
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
                    _reachableTiles = unit.Status.CanMove
                        ? MovementRangeCalculator.GetReachableTiles(
                         unit.Position, unit.Stats.MoveRange, _movementMap, OccupiedTilesExcluding(unit))
                        : new HashSet<GridPosition> { unit.Position };
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

        // Ends the turn without moving or acting. Not a move, so it never arms
        // Momentum and never triggers Bleed.
        private void OnWaitSelected()
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = _battle.CurrentUnit;
            if (unit.Faction != Faction.Player) return;

            Debug.Log($"{unit.DisplayName} waits.");
            _pendingScroll = null;
            EndTurn();
        }

        // Every move goes through here so on-move statuses fire on all paths:
        // player clicks, enemy chasing, enemy roaming. (Momentum now, Bleed in Phase 3.)
        private void MoveUnit(CombatUnit unit, GridPosition destination)
        {
            unit.Position = destination;
            var moveTick = unit.Status.OnMoved();
            foreach (var line in moveTick.Log) Debug.Log(line);
            _unitViews[unit].Refresh();
            if (moveTick.OwnerDied) { HandleDeath(unit); return; }

            foreach (var ended in _battlefieldObjects.RemoveOwnedByMovement(unit))
                Debug.Log($"{unit.DisplayName}'s {ended.Name} fades as they move.");

            ResolveBattlefieldEntry(unit, destination);
        }

        // Fires any Trap/hazard sitting on the tile just entered. Called
        // from every path that changes a unit's position: MoveUnit, and
        // the push/pull/swap resolution below.
        private void ResolveBattlefieldEntry(CombatUnit unit, GridPosition position)
        {
            foreach (var obj in _battlefieldObjects.TriggerOnEntry(unit, position))
            {
                Debug.Log($"{unit.DisplayName} triggers {obj.Name}!");
                foreach (var app in obj.Statuses)
                {
                    var result = unit.Status.Apply(app, obj.OwnerUnit, _rng);
                    if (result.Succeeded)
                        Debug.Log($"{unit.DisplayName} is affected by {obj.Name} ({app.Id}).");
                }
                _unitViews[unit].Refresh();
                if (!unit.IsAlive) { HandleDeath(unit); break; }
            }
            RefreshFogOfWar();
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

            if (SilenceRules.IsBlocked(unit, ability, AbilitySource.Weapon))
            {
                Debug.Log($"{unit.DisplayName} is silenced and can't use {ability.Name}.");
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
            unit.ClampVitals();
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
                case EffectKind.Status:
                    break;
                case EffectKind.Cleanse:
                    int removed = unit.Status.Cleanse(ability.Cleanses);
                    _unitViews[unit].Refresh();
                    Debug.Log($"{unit.DisplayName} uses {ability.Name} on {unit.DisplayName}, removing {removed} status effect(s).");
                    break;
                default:
                    Debug.LogWarning($"{potion.Name}'s ability is {ability.EffectKind}-kind, which isn't meaningful for a self-used potion — nothing happened.");
                    break;
            }
            ApplyAbilityStatuses(unit, unit, ability);
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
            if (SilenceRules.IsBlocked(unit, scroll.Ability, AbilitySource.Scroll))
            {
                Debug.Log($"{unit.DisplayName} is silenced and can't read {scroll.Name}.");
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
                RefreshActionBar(_battle.CurrentUnit);
            }
        }

        private void RefreshActionBar(CombatUnit unit)
        {
            if (unit == null || unit.Faction != Faction.Player) return;

            if (unit.EquippedWeapon != null) _abilityBar.Show(unit.EquippedWeapon, unit.GetRemainingCooldown,
    a => SilenceRules.IsBlocked(unit, a, AbilitySource.Weapon));
            else _abilityBar.ShowMoveOnly();

            SetMode(ActionMode.Move);
        }

        private bool IsInCombat() => _unitViews.Keys.Any(u => u.Faction == Faction.Enemy && u.IsAlive && u.IsAlerted);

        private const int TurnTrackerMinEntries = 5;
        private static bool IsTrackable(CombatUnit u) =>
            u.IsAlive && (u.Faction == Faction.Player || u.IsAlerted);

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

            if (ability.EffectKind == EffectKind.Summon)
                return GetSummonTargetTiles(unit, ability);

            if (ability.AreaShape == AttackShape.Single || ability.AreaShape == AttackShape.Chain)
                return GetSingleTargetTiles(unit, ability);

            return AreaOfEffect.GetAffectedTiles(unit.Position, unit.Position, AttackShape.Blast, ability.Range)
                .Where(InBounds)
                .ToList();
        }

        // A Summon lands on an empty, walkable tile within Range
        // (LOS-gated, same as everything else) — the ANCHOR tile only;
        // the footprint then extends from there via the normal
        // AreaOfEffect geometry (Single for a totem/trap, Line for a
        // wall, Blast/Ring/Cross for a cloud), exactly like a Damage
        // ability's footprint already works.
        private List<GridPosition> GetSummonTargetTiles(CombatUnit unit, IAbility ability)
            => AreaOfEffect.GetAffectedTiles(unit.Position, unit.Position, AttackShape.Blast, ability.Range)
                .Where(pos => InBounds(pos) && _movementMap.IsWalkable(pos) && FindLivingUnitAt(pos) == null
                    && LineOfSight.HasClearPath(unit.Position, pos, _sightMap))
                .ToList();

        // Damage targets enemies; Heal/Buff target allies (the caster's
        // own tile included, so a single-target heal can be cast on
        // yourself).
        private List<GridPosition> GetSingleTargetTiles(CombatUnit unit, IAbility ability)
        {
            bool targetAllies = AbilityTargeting.TargetsAllies(ability);
            var tiles = _unitViews.Keys
                .Where(u => u.IsAlive
                    && (targetAllies ? u.Faction == unit.Faction : u.Faction != unit.Faction)
                    && u.Position.ManhattanDistance(unit.Position) <= ability.Range
                    && LineOfSight.HasClearPath(unit.Position, u.Position, _sightMap))
                .Select(u => u.Position)
                .ToList();

            // A destructible BattlefieldObject (a totem) is also a legal
            // Damage-ability target — checked here so it shows up as a
            // clickable/highlighted tile alongside living units.
            if (ability.EffectKind == EffectKind.Damage)
                tiles.AddRange(_battlefieldObjects.Active
                    .Where(o => o.MaxHp.HasValue
                        && o.Tiles.Any(t => unit.Position.ManhattanDistance(t) <= ability.Range
                            && LineOfSight.HasClearPath(unit.Position, t, _sightMap)))
                    .SelectMany(o => o.Tiles));

            // Taunted: if the taunter is a legal target right now, they're the ONLY one.
            // (Out of range/LOS -> no restriction. Area abilities are never restricted.)
            if (!targetAllies)
            {
                var forced = TauntRules.GetForcedTarget(unit);
                if (forced != null && tiles.Contains(forced.Position))
                    return new List<GridPosition> { forced.Position };
            }

            return tiles;
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

        // A drag-and-drop from the inventory panel. amount is how many to move
        // (int.MaxValue means "the whole stack" — TryMove clamps it). Shuffling
        // the stash (or one belt's order) is always free. Anything that changes
        // what a unit wears or carries follows the normal equip rule: free out
        // of combat, costs the turn once an enemy is alerted — and in combat
        // only the acting unit's own gear/belt can be touched.
        private void OnInventoryMoveRequested(SlotRef from, SlotRef to, int amount)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var current = _battle.CurrentUnit;
            if (current.Faction != Faction.Player) return;

            if (IsInCombat())
            {
                int currentIndex = _partyMembers.IndexOf(current);
                if ((from.BelongsToUnit && from.Owner != currentIndex)
                    || (to.BelongsToUnit && to.Owner != currentIndex))
                {
                    Debug.Log("Only the acting unit's gear can be changed during combat.");
                    _inventoryPanel.Refresh();
                    return;
                }
            }

            var outcome = InventoryTransfer.TryMove(_inventory, _partyMembers, from, to, amount, out var reason);
            if (outcome == MoveOutcome.Rejected)
            {
                if (reason != null) Debug.Log(reason);
                _inventoryPanel.Refresh();
                return;
            }

            if (outcome == MoveOutcome.LoadoutChanged) ResolveEquipCost();
            else _inventoryPanel.Refresh();
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
            if (!acting.Status.CanMove)
            {
                if (target.Equals(acting.Position))
                {
                    Debug.Log($"{acting.DisplayName} is rooted and holds position.");
                    EndTurn();
                }
                return;
            }

            var occupant = FindLivingUnitAt(target);
            if (occupant == null && _reachableTiles.Contains(target))
            {
                MoveUnit(acting, target);
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

        // How many rounds a pack keeps chasing a stale sighting before
        // giving up on it — see PickKnownTarget.
        private const int MemoryMaxAgeRounds = 5;

        // How much OLDER than MemoryMaxAgeRounds a sighting is still
        // allowed to be before it's too cold even to search near — see
        // PickSearchAnchor. Must be greater than MemoryMaxAgeRounds:
        // this is the extra grace period after active chasing ends,
        // not a replacement for it.
        private const int SearchGiveUpRounds = 15;

        // How far from a search anchor an enemy is willing to wander
        // while sniffing around for its lost target — see SearchAI.
        private const int SearchRadius = 5;

        private void RunEnemyTurn(CombatUnit enemy)
        {
            // Waking up is still keyed off the nearest player regardless of this
            // enemy's preferred-target strategy — noticing someone and deciding
            // who to act on are different questions.
            var nearest = FindNearestPlayer(enemy);
            if (nearest == null)
            {
                EndTurn();
                return;
            }

            if (!enemy.IsAlerted)
            {
                if (enemy.CanSense(nearest.Position, _sightMap))
                {
                    AlertUnit(enemy);
                    Debug.Log($"{enemy.DisplayName} spots {nearest.DisplayName}!");
                    RecordSighting(enemy, nearest);
                }
                else
                {
                    if (enemy.Status.CanMove)
                    {
                        var step = RoamAI.ChooseStep(enemy, _movementMap, OccupiedTilesExcluding(enemy), _rng);
                        if (step.HasValue) MoveUnit(enemy, step.Value);
                    }
                    EndTurn();
                    return;
                }
            }

            // Refresh memory for every hostile this enemy can currently
            // see, independent of who it ends up acting on this turn —
            // and share each sighting with the rest of its alerted pack
            // (see RecordSighting). This is what lets an ally who never
            // personally spotted anyone still know where to converge.
            foreach (var hostile in _unitViews.Keys.Where(u => u.IsAlive && u.Faction != enemy.Faction))
                if (enemy.CanSense(hostile.Position, _sightMap))
                    RecordSighting(enemy, hostile);

            // 1. Support: the first weapon ability (in slot order) that targets
            // allies AND has a genuinely needy target within TargetingRange wins
            // — used even if that means moving instead of attacking this turn.
            // Allies aren't subject to fog-of-memory — an enemy always
            // knows exactly how hurt its own side is.
            if (TryPickSupportAction(enemy, out var supportAbility, out var supportTarget))
            {
                ActOnTarget(enemy, supportTarget.Position, supportAbility);
                return;
            }

            // 2. Offense. Taunt always uses the live position — being
            // taunted means the target is right in the attacker's face,
            // not something to look up in memory. Otherwise, chase
            // whatever this enemy's pack has actually SEEN: live if
            // currently visible, else the freshest remembered sighting
            // within MemoryMaxAgeRounds. Nothing chase-worthy falls
            // through to searching near the last thing anyone in the
            // pack saw (PickSearchAnchor, a longer/looser window than
            // chasing), and finally to plain idle roaming once even
            // that's gone cold — the trail is genuinely lost.
            var forced = TauntRules.GetForcedTarget(enemy);
            if (forced != null)
            {
                ActOnTarget(enemy, forced.Position, GetEnemyOffenseAbility(enemy));
                return;
            }

            var (target, believedPosition) = PickKnownTarget(enemy);
            if (target != null)
            {
                ActOnTarget(enemy, believedPosition, GetEnemyOffenseAbility(enemy));
                return;
            }

            var searchAnchor = PickSearchAnchor(enemy);
            if (searchAnchor != null)
            {
                Debug.Log($"{enemy.DisplayName} searches near where it last saw something.");
                if (enemy.Status.CanMove)
                {
                    var step = SearchAI.ChooseStep(
                        enemy, searchAnchor.Value, SearchRadius, _movementMap, OccupiedTilesExcluding(enemy), _rng);
                    if (step.HasValue) MoveUnit(enemy, step.Value);
                }
                EndTurn();
                return;
            }

            // Fully cold — no sighting even within the search window.
            // Settle back into ordinary idle roaming, the same behavior
            // this unit had before it was ever alerted. It stays
            // IsAlerted (no de-aggro, by design — see CombatUnit), so a
            // fresh sighting instantly snaps it back into chasing; it's
            // only the MOVEMENT that reverts to idle in the meantime.
            Debug.Log($"{enemy.DisplayName} gives up the search.");
            if (enemy.Status.CanMove)
            {
                var roamStep = RoamAI.ChooseStep(enemy, _movementMap, OccupiedTilesExcluding(enemy), _rng);
                if (roamStep.HasValue) MoveUnit(enemy, roamStep.Value);
            }
            EndTurn();
        }

        // The nearest remembered sighting of ANY hostile that's still
        // within SearchGiveUpRounds — a longer, looser window than
        // PickKnownTarget's chase threshold. Where an enemy wanders
        // while it no longer has anything worth beelining toward but
        // hasn't fully given up either. Null once every sighting a pack
        // holds has aged past even this.
        private GridPosition? PickSearchAnchor(CombatUnit enemy)
        {
            var anchors = _unitViews.Keys
                .Where(u => u.Faction != enemy.Faction)
                .Select(u => enemy.Memory.Get(u, _battle.RoundNumber, SearchGiveUpRounds))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            return anchors.Count > 0
                ? anchors.OrderBy(p => enemy.Position.ManhattanDistance(p)).First()
                : (GridPosition?)null;
        }

        // Records that `observer` currently sees `target` at its live
        // position, and shares that sighting with every other alerted
        // member of observer's faction — a spotted intruder's position
        // travels through the pack the same way AlertUnit's wake-up
        // already does. Only call this when observer genuinely has
        // current LOS to target (callers already check via CanSense).
        private void RecordSighting(CombatUnit observer, CombatUnit target)
        {
            int round = _battle.RoundNumber;
            foreach (var ally in _unitViews.Keys.Where(u => u.Faction == observer.Faction && u.IsAlive && u.IsAlerted))
                ally.Memory.Record(target, target.Position, round);
        }

        // What this enemy's pack actually knows about who to chase.
        // Prefers a currently-visible hostile (still respecting the
        // configured TargetingStrategy when the strategy's own pick is
        // among what's visible); otherwise falls back to whichever
        // hostile has the nearest FRESH remembered sighting. Returns
        // (null, default) when nothing is known at all — a real "lost
        // the trail" state, not just "nobody's close enough yet."
        private (CombatUnit Target, GridPosition Position) PickKnownTarget(CombatUnit enemy)
        {
            var visibleHostiles = _unitViews.Keys
                .Where(u => u.IsAlive && u.Faction != enemy.Faction && enemy.CanSense(u.Position, _sightMap))
                .ToList();

            if (visibleHostiles.Count > 0)
            {
                var strategyPick = PickOffenseTarget(enemy);
                var chosen = strategyPick != null && visibleHostiles.Contains(strategyPick)
                    ? strategyPick
                    : visibleHostiles.OrderBy(u => u.Position.ManhattanDistance(enemy.Position)).First();
                return (chosen, chosen.Position);
            }

            var remembered = _unitViews.Keys
                .Where(u => u.IsAlive && u.Faction != enemy.Faction)
                .Select(u => (unit: u, pos: enemy.Memory.Get(u, _battle.RoundNumber, MemoryMaxAgeRounds)))
                .Where(p => p.pos.HasValue)
                .ToList();
            if (remembered.Count == 0) return (null, default);

            var best = remembered.OrderBy(p => enemy.Position.ManhattanDistance(p.pos.Value)).First();
            return (best.unit, best.pos.Value);
        }

        // Shared tail: act now if believedPosition is already in range
        // and visible, otherwise take one step toward a tile that would
        // put it in range. A null ability still lets a stuck enemy close
        // distance (range defaults to 1 — melee) rather than doing
        // nothing. believedPosition may be stale (a remembered sighting
        // rather than a live one) — if whoever's actually there (if
        // anyone) isn't the target, ExecuteAbilityAt simply finds no
        // living unit at that tile and the attack fizzles; that's
        // correct, not a bug.
        private void ActOnTarget(CombatUnit enemy, GridPosition believedPosition, IAbility ability)
        {
            int range = ability?.Range ?? 1;

            if (enemy.Position.ManhattanDistance(believedPosition) <= range
                && LineOfSight.HasClearPath(enemy.Position, believedPosition, _sightMap))
            {
                if (ability != null) ExecuteAbilityAt(enemy, ability, believedPosition);
                EndTurn();
                return;
            }

            if (enemy.Status.CanMove)
            {
                var reachable = MovementRangeCalculator.GetReachableTiles(
                    enemy.Position, enemy.Stats.MoveRange, _movementMap, OccupiedTilesExcluding(enemy));
                if (reachable.Count > 0)
                {
                    var step = ChooseEnemyStep(enemy, believedPosition, range, reachable);
                    if (step.HasValue) MoveUnit(enemy, step.Value);
                }
            }

            EndTurn();
        }

        // The first weapon ability (in order) that's usable, targets allies,
        // and has at least one ally within TargetingRange who actually needs
        // it right now. False (both out params null) is the normal case for
        // most turns — most of the time nobody needs anything.
        private bool TryPickSupportAction(CombatUnit enemy, out IAbility ability, out CombatUnit target)
        {
            ability = null;
            target = null;
            var weapon = enemy.EquippedWeapon;
            if (weapon == null) return false;

            var allies = AlliesWithinRange(enemy, enemy.TargetingRange);
            if (allies.Count == 0) return false;

            foreach (var candidate in weapon.Abilities)
            {
                if (enemy.IsOnCooldown(candidate)) continue;
                if (SilenceRules.IsBlocked(enemy, candidate, AbilitySource.Weapon)) continue;
                if (!AbilityTargeting.TargetsAllies(candidate)) continue;

                var needy = allies.Where(a => SupportNeed.Needs(a, candidate, enemy.SupportHealThreshold)).ToList();
                if (needy.Count == 0) continue;

                var picked = EnemyTargeting.PickSupportTarget(enemy, needy, enemy.SupportTargetingStrategy);
                if (picked != null)
                {
                    ability = candidate;
                    target = picked;
                    return true;
                }
            }

            return false;
        }

        // The offense counterpart of the ally scan above: living enemies within
        // this unit's TargetingRange, picked by its own TargetingStrategy. Null
        // (not empty) when nobody qualifies. NOTE: unlike PickKnownTarget, this
        // does not itself check current visibility — PickKnownTarget guards
        // that by only accepting this pick when it's also in visibleHostiles.
        private CombatUnit PickOffenseTarget(CombatUnit enemy)
        {
            var hostiles = _unitViews.Keys
                .Where(u => u.IsAlive && u.Faction != enemy.Faction
                    && u.Position.ManhattanDistance(enemy.Position) <= enemy.TargetingRange)
                .ToList();

            return hostiles.Count > 0
                ? EnemyTargeting.PickHostileTarget(enemy, hostiles, enemy.TargetingStrategy)
                : null;
        }

        private List<CombatUnit> AlliesWithinRange(CombatUnit enemy, int range)
    => _unitViews.Keys
        .Where(u => u.IsAlive && u != enemy && u.Faction == enemy.Faction
            && u.Position.ManhattanDistance(enemy.Position) <= range)
        .ToList();

        // Prefers a reachable tile that already has range AND line of
        // sight to believedPosition — "find a firing position" — breaking
        // ties by real path distance. Falls back to the reachable tile
        // with the smallest real path distance if no such tile exists
        // this turn. Either way, "real path distance" (via
        // PathDistanceField) is what actually fixes walking into
        // dead-end corners — a straight-line-close tile behind a wall
        // now correctly ranks farther than one along an actual route
        // around it.
        private GridPosition? ChooseEnemyStep(
            CombatUnit enemy, GridPosition believedPosition, int range, HashSet<GridPosition> reachable)
        {
            var distanceField = PathDistanceField.BuildFrom(believedPosition, _movementMap);

            var firingPosition = reachable
                .Where(pos => pos.ManhattanDistance(believedPosition) <= range
                    && LineOfSight.HasClearPath(pos, believedPosition, _sightMap))
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

            if (ability.EffectKind == EffectKind.Summon)
            {
                SpawnBattlefieldObject(attacker, ability, impactTile);
                return;
            }

            if (ability.AreaShape == AttackShape.Single)
            {
                var target = FindLivingUnitAt(impactTile);
                if (target != null) { ApplyAbilityEffect(attacker, target, ability); return; }

                if (ability.EffectKind == EffectKind.Damage)
                {
                    var obj = _battlefieldObjects.At(impactTile).FirstOrDefault(o => o.MaxHp.HasValue);
                    if (obj != null) DamageBattlefieldObject(attacker, obj, ability);
                }
                return;
            }

            if (ability.AreaShape == AttackShape.Chain)
            {
                ExecuteChain(attacker, ability, impactTile);
                return;
            }

            var affectedTiles = new HashSet<GridPosition>(
                AreaOfEffect.GetAffectedTiles(attacker.Position, impactTile, ability.AreaShape, ability.AreaRadius));



            // Damage AoE excludes the caster (allies can still be caught
            // in it, same as most tactics games); Heal/Buff AoE includes
            // them, since standing in your own heal nova should heal
            // you too.
            bool excludeCaster = !AbilityTargeting.TargetsAllies(ability);

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

                case EffectKind.Status:
                    // No primary effect — the ability's Statuses are the whole payload.
                    break;

                case EffectKind.Cleanse:
                    int removed = target.Status.Cleanse(ability.Cleanses);
                    _unitViews[target].Refresh();
                    Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {target.DisplayName}, removing {removed} status effect(s).");
                    break;
            }

            ApplyAbilityStatuses(attacker, target, ability);   // <-- new, after the switch's closing brace
            ApplyPositionalEffects(attacker, target, ability);
        }

        private void ApplyPositionalEffects(CombatUnit attacker, CombatUnit target, IAbility ability)
        {
            if (target == attacker || !target.IsAlive) return;

            if (ability.SwapWithCaster)
            {
                var casterPos = attacker.Position;
                attacker.Position = target.Position;
                target.Position = casterPos;
                _unitViews[attacker].Refresh();
                _unitViews[target].Refresh();
                Debug.Log($"{attacker.DisplayName} swaps places with {target.DisplayName}.");
                ResolveBattlefieldEntry(attacker, attacker.Position);
                ResolveBattlefieldEntry(target, target.Position);
                return;
            }

            if (ability.PushDistance != 0)
                ApplyPush(attacker, target, ability.PushDistance);
        }

        // Moves `target` up to Abs(distance) tiles along the
        // attacker->target line — away from the attacker if positive,
        // toward it if negative — stopping early at the first wall or
        // occupied tile. No bonus damage for a "wall splat".
        private void ApplyPush(CombatUnit attacker, CombatUnit target, int distance)
        {
            var (dx, dy) = CardinalDirectionFrom(attacker.Position, target.Position);
            bool pulling = distance < 0;
            var current = target.Position;

            for (int i = 0; i < Math.Abs(distance); i++)
            {
                var next = pulling
                    ? new GridPosition(current.X - dx, current.Y - dy)
                    : new GridPosition(current.X + dx, current.Y + dy);
                if (!_movementMap.IsWalkable(next) || FindLivingUnitAt(next) != null) break;
                current = next;
            }

            if (current.Equals(target.Position)) return;

            target.Position = current;
            _unitViews[target].Refresh();
            Debug.Log($"{target.DisplayName} is {(pulling ? "pulled" : "pushed")} to ({current.X},{current.Y}).");
            ResolveBattlefieldEntry(target, current);
        }

        // Same cardinal-snap rule AreaOfEffect uses for Line/Cone,
        // duplicated here (it's private there) rather than exposed.
        private static (int dx, int dy) CardinalDirectionFrom(GridPosition from, GridPosition to)
        {
            int dx = to.X - from.X, dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy)
                ? (Math.Sign(dx) == 0 ? 1 : Math.Sign(dx), 0)
                : (0, Math.Sign(dy));
        }

        // Applies the ability's Statuses to one target: the whole payload for
        // EffectKind.Status, on-hit procs for every other kind. Skipped if the
        // target is already dead (a Damage hit may have just killed it).
        private void ApplyAbilityStatuses(CombatUnit source, CombatUnit target, IAbility ability)
        {
            var statuses = ability.Statuses;
            if (statuses == null || statuses.Count == 0 || !target.IsAlive) return;

            foreach (var app in statuses)
            {
                var rule = StatusRules.Get(app.Id);
                var result = target.Status.Apply(app, source, _rng);

                // A hostile status counts as an attack even if it fails to land.
                if (rule.Polarity == StatusPolarity.Harmful && target.Faction != source.Faction)
                    AlertUnit(target);

                switch (result.Outcome)
                {
                    case StatusApplyOutcome.Applied:
                    case StatusApplyOutcome.Stacked:
                    case StatusApplyOutcome.Refreshed:
                        Debug.Log($"{target.DisplayName} is affected by {app.Id} ({result.Outcome}).");
                        break;
                    case StatusApplyOutcome.Resisted:
                        Debug.Log($"{target.DisplayName} resists {app.Id}.");
                        break;
                        // ProcFailed: silent — an on-hit proc that didn't trigger isn't news.
                }
            }

            _unitViews[target].Refresh();
        }

        // Hits whoever's at impactTile, then repeatedly jumps to the nearest
        // not-yet-hit valid target within `ability.Range` of the PREVIOUS
        // target, up to `ability.AreaRadius` additional jumps (AreaRadius 2 =
        // 3 targets total: the initial hit plus 2 jumps). "Valid" follows the
        // ability's own polarity — enemies for a hostile ability, allies for a
        // beneficial one. Fizzles early (fewer targets than the max) once
        // nothing eligible remains in range — that's normal, not an error.
        //
        // DESIGN NOTE: this reuses Range/AreaRadius rather than adding new
        // IAbility fields — Range becomes "max distance per jump" (same
        // meaning as always: how far this ability can reach) and AreaRadius
        // becomes "how many further jumps," instead of a physical radius. Say
        // if you'd rather Chain had its own dedicated field(s) instead.
        private void ExecuteChain(CombatUnit attacker, IAbility ability, GridPosition impactTile)
        {
            var first = FindLivingUnitAt(impactTile);
            if (first == null) return;

            bool targetsAllies = AbilityTargeting.TargetsAllies(ability);
            var hit = new HashSet<CombatUnit> { first };
            ApplyAbilityEffect(attacker, first, ability);

            var current = first;
            for (int jump = 0; jump < ability.AreaRadius; jump++)
            {
                var next = _unitViews.Keys
                    .Where(u => u.IsAlive && !hit.Contains(u)
                        && (targetsAllies ? u.Faction == attacker.Faction : u.Faction != attacker.Faction)
                        && u.Position.ManhattanDistance(current.Position) <= ability.Range
                        && LineOfSight.HasClearPath(current.Position, u.Position, _sightMap))
                    .OrderBy(u => u.Position.ManhattanDistance(current.Position))
                    .FirstOrDefault();

                if (next == null) break;
                hit.Add(next);
                ApplyAbilityEffect(attacker, next, ability);
                current = next;
            }
        }

        private void SpawnBattlefieldObject(CombatUnit caster, IAbility ability, GridPosition anchorTile)
        {
            var tiles = AreaOfEffect.GetAffectedTiles(caster.Position, anchorTile, ability.AreaShape, ability.AreaRadius);
            var obj = new BattlefieldObject
            {
                Name = ability.Name,
                OwnerFaction = caster.Faction,
                OwnerUnit = ability.EndsIfOwnerMoves ? caster : null,
                Tiles = tiles,
                RemainingDuration = ability.SummonDuration > 0 ? ability.SummonDuration : int.MaxValue,
                MaxHp = ability.SummonMaxHp > 0 ? ability.SummonMaxHp : (int?)null,
                CurrentHp = ability.SummonMaxHp,
                BlocksMovement = ability.SummonBlocksMovement,
                BlocksLineOfSight = ability.SummonBlocksLineOfSight,
                IsHidden = ability.SummonIsHidden,
                TriggerMode = ability.SummonTriggerMode,
                ConsumedAfterTrigger = ability.SummonConsumedAfterTrigger,
                IgnoreOwnerFaction = ability.SummonIgnoreOwnerFaction,
                AuraRadius = ability.AuraRadius,
                AffectsAllies = ability.SummonAffectsAllies,
                Statuses = ability.Statuses
            };
            _battlefieldObjects.Add(obj);
            Debug.Log($"{caster.DisplayName} places {ability.Name}.");
            RefreshFogOfWar();
        }

        // SIMPLIFICATION, flagged deliberately: this does NOT go through
        // AttackResolver — an inanimate object has no defense stat or
        // crit chance to resolve against. Damage is just Attack x
        // Multiplier, rounded, minimum 1.
        private void DamageBattlefieldObject(CombatUnit attacker, BattlefieldObject obj, IAbility ability)
        {
            float attackStat = ability.Kind == AttackKind.Physical
                ? attacker.Stats.PhysicalAttack : attacker.Stats.MagicAttack;
            int damage = Math.Max(1, (int)Math.Round(attackStat * ability.DamageMultiplier));

            obj.CurrentHp -= damage;
            Debug.Log($"{attacker.DisplayName} hits {obj.Name} for {damage}.");

            if (obj.CurrentHp <= 0)
            {
                _battlefieldObjects.Remove(obj);
                Debug.Log($"{obj.Name} is destroyed!");
            }
            RefreshFogOfWar();
        }

        private void TickBattlefieldObjects()
        {
            foreach (var expired in _battlefieldObjects.TickDuration())
                Debug.Log($"{expired.Name} fades away.");

            foreach (var obj in _battlefieldObjects.Active.ToList())
            {
                if (obj.TriggerMode != SummonTriggerMode.OnRoundTick) continue;

                foreach (var unit in _battlefieldObjects.UnitsInAuraRange(obj, _unitViews.Keys))
                {
                    foreach (var app in obj.Statuses)
                    {
                        var result = unit.Status.Apply(app, obj.OwnerUnit, _rng);
                        if (result.Succeeded)
                            Debug.Log($"{unit.DisplayName} is affected by {obj.Name} ({app.Id}).");
                    }
                    _unitViews[unit].Refresh();
                }
            }

            RefreshFogOfWar();
        }

        // Optional visual layer: spawns/destroys one plain marker per
        // occupied tile of every currently-visible (non-hidden)
        // BattlefieldObject. No-ops entirely until you assign
        // _battlefieldObjectMarkerPrefab in the Inspector. Swap the
        // Instantiate call for a real BattlefieldObjectView component
        // when you want per-object sprites/tinting/duration display.
        private void RefreshBattlefieldViews()
        {
            var stale = _battlefieldViews.Keys.Where(o => !_battlefieldObjects.Active.Contains(o)).ToList();
            foreach (var obj in stale)
            {
                foreach (var view in _battlefieldViews[obj]) Destroy(view);
                _battlefieldViews.Remove(obj);
            }

            if (_battlefieldObjectMarkerPrefab == null) return;

            foreach (var obj in _battlefieldObjects.Visible)
            {
                if (_battlefieldViews.ContainsKey(obj)) continue;
                var views = new List<GameObject>();
                foreach (var tile in obj.Tiles)
                {
                    var marker = Instantiate(_battlefieldObjectMarkerPrefab, transform);
                    marker.transform.position = GridToWorld.ToWorldPosition(tile);
                    views.Add(marker);
                }
                _battlefieldViews[obj] = views;
            }
        }

        // The single place fog gets recomputed and every dependent view
        // gets updated: tile shading, which enemies/health bars are
        // shown, danger-zone markers, loot markers, and battlefield
        // object markers. Call after anything that could change what's
        // visible — a unit moves or dies, or a Wall/Cloud appears,
        // vanishes, or is destroyed. Cheap enough to call liberally;
        // see FogOfWar.Recompute.
        private void RefreshFogOfWar()
        {
            var players = _unitViews.Keys.Where(u => u.Faction == Faction.Player);
            _fog.Recompute(players, _sightMap, _map);

            _gridView.ApplyFog(_fog);

            foreach (var unit in _unitViews.Keys)
            {
                // The player's own party is always shown; only the
                // opposing side is gated by current vision — you always
                // know where your own people are.
                bool visible = unit.Faction == Faction.Player || _fog.IsCurrentlyVisible(unit.Position);
                _unitViews[unit].SetFogVisible(visible);
                if (_healthBars.TryGetValue(unit, out var bar)) bar.SetFogVisible(visible);
            }

            RefreshDangerZoneMarkers();
            RefreshLootMarkerVisibility();
            RefreshBattlefieldViews();
            RefreshBattlefieldMarkerVisibility();
        }

        // A loot pile/chest only shows while its tile is currently lit —
        // same rule as an enemy unit. It reappears (not re-triggers
        // anything) the moment it's back in view; nothing is destroyed
        // here, only shown/hidden.
        private void RefreshLootMarkerVisibility()
        {
            foreach (var pair in _lootMarkers)
                pair.Value.SetActive(_fog.IsCurrentlyVisible(pair.Key));

            foreach (var chest in _chests)
                if (chest != null)
                {
                    var renderer = chest.GetComponent<SpriteRenderer>();
                    if (renderer != null) renderer.enabled = _fog.IsCurrentlyVisible(chest.Position);
                }
        }

        // Same idea for totem/wall/cloud markers — each marker's tile is
        // obj.Tiles[i] in the same order RefreshBattlefieldViews created
        // them, so they're zipped by index rather than needing the tile
        // stored on the marker itself.
        private void RefreshBattlefieldMarkerVisibility()
        {
            foreach (var pair in _battlefieldViews)
            {
                var tiles = pair.Key.Tiles;
                var views = pair.Value;
                for (int i = 0; i < views.Count && i < tiles.Count; i++)
                    views[i].SetActive(_fog.IsCurrentlyVisible(tiles[i]));
            }
        }

        private void ResolveAttack(CombatUnit attacker, CombatUnit defender, IAbility ability)
        {
            var result = AttackResolver.Resolve(attacker, defender, ability, _rng);
            bool defenderWasAlive = defender.IsAlive;
            bool attackerWasAlive = attacker.IsAlive;

            var outcome = DamagePipeline.Apply(attacker, defender, result.Damage);

            AlertUnit(defender);
            _unitViews[defender].Refresh();
            _unitViews[attacker].Refresh();
            Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {defender.DisplayName} for {outcome.HpLost} {ability.Kind}{(result.IsCrit ? " (CRIT)" : "")}");
            foreach (var line in outcome.Log) Debug.Log(line);

            if (defenderWasAlive && !defender.IsAlive) HandleDeath(defender, attacker);
            if (attackerWasAlive && !attacker.IsAlive) HandleDeath(attacker);   // died to thorns
        }

        // No revive exists — a dead unit is gone for the rest of the run.
        // Whatever it had equipped drops onto its tile as loot, for either
        // side to reclaim.
        // `killer` is whoever landed the killing blow, or null when nobody
        // did (a poison tick, a trap, ...) — it only matters for XP.
        private void HandleDeath(CombatUnit unit, CombatUnit killer = null)
        {
            Debug.Log($"{unit.DisplayName} has fallen.");

            foreach (var ended in _battlefieldObjects.RemoveOwnedByMovement(unit))
                Debug.Log($"{unit.DisplayName}'s {ended.Name} fades as they fall.");

            // A natural weapon/armor (claws, thick hide, etc.) never
            // shows up as loot — only real gear does.
            var droppedWeapon = unit.EquippedWeapon != null && unit.EquippedWeapon.DropsOnDeath
                ? unit.EquippedWeapon : null;
            var droppedArmor = unit.Stats.EquippedArmor != null && unit.Stats.EquippedArmor.DropsOnDeath
                ? unit.Stats.EquippedArmor : null;

            LootDrop pile = null;
            if (droppedWeapon != null || droppedArmor != null)
                pile = new LootDrop(unit.Position, droppedWeapon, droppedArmor);

            // Enemies only: roll their extra consumable drop and pay out
            // their XP. (A dead hero has no entry in _enemyKits.)
            if (_enemyKits.TryGetValue(unit, out var kit))
            {
                _enemyKits.Remove(unit);

                if (DropTable.TryRoll(kit.Drops, kit.DropChance, _rng, out var rolled))
                {
                    pile = pile ?? new LootDrop(unit.Position, null, null);
                    if (rolled.Potion != null) pile.AddPotion(rolled.Potion, rolled.Count);
                    else pile.AddScroll(rolled.Scroll, rolled.Count);
                }

                AwardExperience(unit, kit.XpReward, killer);
            }

            if (pile != null && !pile.IsEmpty)
            {
                _lootOnGround.Add(pile);
                SpawnLootMarker(unit.Position);
            }

            RefreshFogOfWar();
        }

        // The killer earns the full reward; every other LIVING hero earns
        // the party share (numbers live in LevelUpRules). No killer (a
        // status or trap kill), or a killer who isn't a hero, means
        // everyone alive just gets the share. Levels gained bank stat
        // points — spending them is a UI job, so for now this just logs.
        private void AwardExperience(CombatUnit dead, int xpReward, CombatUnit killer)
        {
            if (xpReward <= 0 || _partyMembers == null) return;

            foreach (var hero in _partyMembers)
            {
                if (!hero.IsAlive) continue;

                float multiplier = hero == killer
                    ? LevelUpRules.KillerXpMultiplier
                    : LevelUpRules.PartyShareXpMultiplier;
                int amount = Mathf.Max(1, Mathf.RoundToInt(xpReward * multiplier));

                int levelsGained = hero.Stats.AddExperience(amount);
                Debug.Log($"{hero.DisplayName} gains {amount} XP for {dead.DisplayName} " +
                          $"({hero.Stats.Experience}/{hero.Stats.ExperienceToNextLevel}).");
                if (levelsGained > 0)
                    Debug.Log($"{hero.DisplayName} reached level {hero.Stats.Level}! " +
                              $"{hero.Stats.UnspentPoints} stat point(s) to spend.");
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
                if (!_fog.IsCurrentlyVisible(enemy.Position)) continue; // can't see it, can't see its danger zone
                foreach (var tile in TilesWithinRadius(enemy.Position, enemy.DetectionRadius))
                    if (_map.IsWalkable(tile) && LineOfSight.HasClearPath(enemy.Position, tile, _sightMap))
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

        // Only weapon abilities that DON'T target allies are offense candidates
        // — a Heal-kind ability sitting in slot 0 no longer blocks a Damage
        // ability in slot 1 from ever being tried; support already had its own
        // pass in TryPickSupportAction.
        private IAbility GetEnemyOffenseAbility(CombatUnit enemy)
        {
            var weapon = enemy.EquippedWeapon;
            if (weapon == null) return null;

            foreach (var candidate in weapon.Abilities)
                if (!enemy.IsOnCooldown(candidate)
                    && !SilenceRules.IsBlocked(enemy, candidate, AbilitySource.Weapon)
                    && !AbilityTargeting.TargetsAllies(candidate))
                    return candidate;

            return null;
        }

        private void EndTurn()
        {
            var ending = _battle.CurrentUnit;
            if (ending != null)
            {
                bool wasAlive = ending.IsAlive;
                var endTick = ending.Status.OnTurnEnd();
                foreach (var line in endTick.Log) Debug.Log(line);
                if (wasAlive && !ending.IsAlive)
                {
                    _unitViews[ending].Refresh();
                    HandleDeath(ending);
                }
            }

            _battle.EndCurrentTurn();
            BeginTurn();
        }
    }
}
