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
    /// target or AoE) and ends the turn. A minimal "move toward nearest
    /// enemy, attack if in range" rule controls enemy turns until real AI
    /// gets built as its own piece.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        [SerializeField] private GridView _gridView;
        [SerializeField] private UnitView _unitViewPrefab;
        [SerializeField] private AbilityBarView _abilityBar;
        [SerializeField] private InventoryPanelView _inventoryPanel;
        [SerializeField] private GameObject _inventoryToggleButton;
        [SerializeField] private ItemRegistry _itemRegistry;
        [SerializeField] private int _mapWidth = 24;
        [SerializeField] private int _mapHeight = 16;

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
        }

        // Forwards to the panel's own Toggle() — lets the Inventory
        // button's OnClick target BattleController directly. Opening the
        // panel backs out of any in-progress aim, so a stray click on a
        // grid tile behind the panel can't be misread as a target.
        public void ToggleInventoryPanel()
        {
            if (_inventoryPanel == null) return;
            if (!_inventoryPanel.gameObject.activeSelf && _mode != ActionMode.Move)
                SetMode(ActionMode.Move);
            _inventoryPanel.Toggle();
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
            _inventory.AddWeapon(_itemRegistry.GetWeapon(WeaponId.Bow));
            _inventory.AddWeapon(_itemRegistry.GetWeapon(WeaponId.Staff));
            _inventory.AddArmor(_itemRegistry.GetArmor(ArmorId.Cloak));
            _inventory.AddArmor(_itemRegistry.GetArmor(ArmorId.Robe));
            _inventory.AddPotion(_itemRegistry.GetPotion(PotionId.HealthPotion), 2);
            _inventory.AddScroll(_itemRegistry.GetScroll(ScrollId.Fireball), 1);
        }

        private const int CorridorRoamRadius = 6;

        private List<CombatUnit> BuildStartingRoster(GeneratedDungeon dungeon)
        {
            var playerSpawns = SpawnZones.PlayerSpawns(dungeon, 2);

            var warrior = new CombatUnit("Warrior", Faction.Player,
                new UnitStats(ClassLibrary.Get(ClassId.Warrior)), playerSpawns[0]);
            warrior.TryEquip(_itemRegistry.GetWeapon(WeaponId.Sword));
            warrior.Stats.TryEquipArmor(_itemRegistry.GetArmor(ArmorId.Plate));

            var adept = new CombatUnit("Adept", Faction.Player,
                new UnitStats(ClassLibrary.Get(ClassId.Adept)), playerSpawns[1]);
            adept.TryEquip(_itemRegistry.GetWeapon(WeaponId.Staff));
            adept.Stats.TryEquipArmor(_itemRegistry.GetArmor(ArmorId.Robe));

            var units = new List<CombatUnit> { warrior, adept };
            units.AddRange(BuildRoomGuardGroup(dungeon, playerSpawns));
            units.AddRange(BuildCorridorScoutGroup(dungeon, playerSpawns, units));

            return units;
        }

        // Melee "guards" confined to the last room placed while idle —
        // reuses the existing Warrior/Sword/Plate combo.
        private List<CombatUnit> BuildRoomGuardGroup(GeneratedDungeon dungeon, List<GridPosition> exclude)
        {
            var spawns = SpawnZones.EnemySpawns(dungeon, 2, exclude);
            var room = dungeon.Rooms.Last();
            var roamZone = room.Tiles().ToList();

            var group = new List<CombatUnit>();
            for (int i = 0; i < spawns.Count; i++)
            {
                var grunt = new CombatUnit("Grunt", Faction.Enemy,
                    new UnitStats(ClassLibrary.Get(ClassId.Warrior)), spawns[i]);
                grunt.TryEquip(_itemRegistry.GetWeapon(WeaponId.Sword));
                grunt.Stats.TryEquipArmor(_itemRegistry.GetArmor(ArmorId.Plate));
                grunt.SetRoamZone(roamZone);
                group.Add(grunt);
            }
            return group;
        }

        // Ranged "scouts" confined to a local patch of corridor tiles
        // around a central anchor point while idle — a Scout/Bow/Cloak
        // combo, distinct from the room guards on purpose.
        private List<CombatUnit> BuildCorridorScoutGroup(
            GeneratedDungeon dungeon, List<GridPosition> playerSpawns, List<CombatUnit> alreadyPlaced)
        {
            var anchor = SpawnZones.PickCorridorAnchor(dungeon);
            if (anchor == null) return new List<CombatUnit>(); // no corridors (e.g. single-room fallback)

            var exclude = playerSpawns.Concat(alreadyPlaced.Select(u => u.Position)).ToList();
            var spawns = SpawnZones.SpawnsNearPosition(dungeon, anchor.Value, 2, exclude);
            var roamZone = dungeon.CorridorTilesNear(anchor.Value, CorridorRoamRadius);

            var group = new List<CombatUnit>();
            for (int i = 0; i < spawns.Count; i++)
            {
                var scout = new CombatUnit("Scout", Faction.Enemy,
                    new UnitStats(ClassLibrary.Get(ClassId.Scout)), spawns[i]);
                scout.TryEquip(_itemRegistry.GetWeapon(WeaponId.Bow));
                scout.Stats.TryEquipArmor(_itemRegistry.GetArmor(ArmorId.Cloak));
                scout.SetRoamZone(roamZone);
                group.Add(scout);
            }
            return group;
        }

        private void SpawnUnitView(CombatUnit unit)
        {
            var view = Instantiate(_unitViewPrefab, transform);
            char symbol = unit.DisplayName[0];
            view.Bind(unit, symbol);
            _unitViews[unit] = view;
        }

        private void BeginTurn()
        {
            if (_battle.Outcome != BattleOutcome.InProgress)
            {
                Debug.Log($"Battle over: {_battle.Outcome}");
                _abilityBar.Hide();
                _inventoryPanel.Hide();
                _inventoryToggleButton.SetActive(false);
                return;
            }

            var unit = _battle.CurrentUnit;
            Debug.Log($"Round {_battle.RoundNumber} — {unit.DisplayName}'s turn ({unit.Faction})");
            RefreshTurnHighlight();
            _gridView.ClearHighlights();
            _pendingScroll = null;
            _inventoryPanel.Hide();

            if (unit.Faction == Faction.Player)
            {
                _inventoryToggleButton.SetActive(true);
                _inventoryPanel.SetTargetMode(IsInCombat(), _partyMembers.IndexOf(unit));

                if (unit.EquippedWeapon != null) _abilityBar.Show(unit.EquippedWeapon);
                else _abilityBar.ShowMoveOnly();

                SetMode(ActionMode.Move);
            }
            else
            {
                _inventoryToggleButton.SetActive(false);
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
        private void OnPotionUseRequested(IPotion potion, int targetIndex)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = ResolveEquipTarget(targetIndex);
            if (unit == null) return;
            if (!potion.CanUse(unit.Stats.Current))
            {
                Debug.Log($"{unit.DisplayName} cannot use {potion.Name} — stat requirement not met");
                return;
            }
            if (!_inventory.TryConsumePotion(potion)) return;

            unit.Heal(potion.HealHp);
            unit.RestoreMp(potion.HealMp);
            _unitViews[unit].Refresh();
            Debug.Log($"{unit.DisplayName} drinks {potion.Name} (+{potion.HealHp} HP, +{potion.HealMp} MP)");

            ResolveEquipCost();
        }

        // Scrolls are different: reading one aims its ability at the
        // grid just like a weapon ability, rather than resolving right
        // away. The scroll is only actually consumed once a valid
        // target tile is clicked (see HandleAbilityClick) — cancelling
        // via Move/right-click/Escape leaves it in the inventory.
        private void OnScrollUseRequested(IScroll scroll, int targetIndex)
        {
            if (_battle == null || _battle.Outcome != BattleOutcome.InProgress) return;
            var unit = ResolveEquipTarget(targetIndex);
            if (unit == null || unit != _battle.CurrentUnit)
            {
                Debug.Log("Can only use items for whoever's turn it currently is.");
                return;
            }
            if (scroll.Ability == null) return;
            if (!scroll.CanUse(unit.Stats.Current))
            {
                Debug.Log($"{unit.DisplayName} cannot read {scroll.Name} — stat requirement not met");
                return;
            }

            _pendingScroll = scroll;
            _inventoryPanel.Hide();
            SetMode(ActionMode.Scroll);
        }

        // The switcher only ever offers an index within the party list,
        // and locks to the current unit's index once in combat — so this
        // should never actually return null in practice, but a mis-wired
        // scene (e.g. wrong party size) fails safe instead of throwing.
        private CombatUnit ResolveEquipTarget(int partyIndex)
        {
            if (_partyMembers == null || partyIndex < 0 || partyIndex >= _partyMembers.Count) return null;
            return _partyMembers[partyIndex];
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
        // ability can only be clicked on a living enemy within Range; an
        // AoE ability (Blast/Line/Cone) can be aimed at ANY in-bounds
        // tile within Range, occupied or not, since the impact point —
        // not an occupant — is what matters.
        private List<GridPosition> GetValidAbilityTargetTiles(CombatUnit unit, IAbility ability)
        {
            if (ability == null) return new List<GridPosition>();

            if (ability.AreaShape == AttackShape.Single)
                return GetAttackableEnemyTiles(unit, ability);

            return AreaOfEffect.GetAffectedTiles(unit.Position, unit.Position, AttackShape.Blast, ability.Range)
                .Where(InBounds)
                .ToList();
        }

        private List<GridPosition> GetAttackableEnemyTiles(CombatUnit unit, IAbility ability)
        {
            return _unitViews.Keys
                .Where(u => u.IsAlive && u.Faction != unit.Faction
                    && u.Position.ManhattanDistance(unit.Position) <= ability.Range)
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
            if (isScroll) _inventory.TryConsumeScroll(_pendingScroll);
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
                if (enemy.CanSense(target.Position))
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

            if (enemy.Position.ManhattanDistance(target.Position) <= range)
            {
                if (ability != null) ExecuteAbilityAt(enemy, ability, target.Position);
                EndTurn();
                return;
            }

            var reachable = MovementRangeCalculator.GetReachableTiles(
                enemy.Position, enemy.Stats.MoveRange, _map, OccupiedTilesExcluding(enemy));
            if (reachable.Count > 0)
            {
                var step = reachable.OrderBy(pos => pos.ManhattanDistance(target.Position)).First();
                enemy.Position = step;
                _unitViews[enemy].Refresh();
            }

            EndTurn();
        }

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

        // Dispatches to single-target or AoE resolution depending on the
        // ability's shape — the one place both HandleAbilityClick and
        // RunEnemyTurn go through, so an enemy wielding an AoE weapon
        // "just works" the same way a player's does.
        private void ExecuteAbilityAt(CombatUnit attacker, IAbility ability, GridPosition impactTile)
        {
            if (ability.AreaShape == AttackShape.Single)
            {
                var defender = FindLivingUnitAt(impactTile);
                if (defender == null) return;
                ResolveAttack(attacker, defender, ability);
                return;
            }

            var hits = AttackResolver.ResolveAoE(attacker, ability, impactTile, _unitViews.Keys, _rng);
            foreach (var (target, result) in hits)
            {
                target.ApplyDamage(result.Damage);
                AlertUnit(target);
                _unitViews[target].Refresh();
                Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {target.DisplayName} for {result.Damage} {ability.Kind}{(result.IsCrit ? " (CRIT)" : "")} (AoE)");
            }
        }

        private void ResolveAttack(CombatUnit attacker, CombatUnit defender, IAbility ability)
        {
            var result = AttackResolver.Resolve(attacker, defender, ability, _rng);
            defender.ApplyDamage(result.Damage);
            AlertUnit(defender);
            _unitViews[defender].Refresh();
            Debug.Log($"{attacker.DisplayName} uses {ability.Name} on {defender.DisplayName} for {result.Damage} {ability.Kind}{(result.IsCrit ? " (CRIT)" : "")}");
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

        private IAbility GetEnemyActiveAbility(CombatUnit unit)
        {
            var weapon = unit.EquippedWeapon;
            return weapon != null && weapon.Abilities.Count > 0 ? weapon.Abilities[0] : null;
        }

        private void EndTurn()
        {
            _battle.EndCurrentTurn();
            BeginTurn();
        }
    }
}
