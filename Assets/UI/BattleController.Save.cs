using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using DungeonTower.Combat;
using DungeonTower.Core;
using DungeonTower.Generation;
using DungeonTower.Items;

namespace DungeonTower.UI
{
    /// <summary>
    /// Saving and loading a game in progress. A save is a snapshot of the
    /// floor you're standing on: the map and what you've explored, every
    /// hero and living enemy (stats, gear, HP/MP, statuses, cooldowns,
    /// belts), traps/totems/walls, chests, loot on the ground, the shared
    /// inventory and where the turn stands.
    ///
    /// You can save whenever it's your hero phase and nothing is animating
    /// (CanSaveNow). That's a clean moment: the enemies are idle, so there's
    /// no half-played enemy turn to capture. Loading drops you back in the
    /// same hero phase.
    ///
    /// Enemies are rebuilt from their EnemySO and then given back their
    /// saved HP, statuses and so on, so a balance change to an enemy asset
    /// applies to loaded games too. Heroes are rebuilt from their saved stats.
    /// </summary>
    public sealed partial class BattleController
    {
        [Header("Saving")]
        [Tooltip("The pause menu. Assigning it turns on its Save buttons.")]
        [SerializeField] private PauseMenuView _pauseMenu;
        [Tooltip("The starter kit from character creation. Lets a save find the items heroes start with.")]
        [SerializeField] private StarterKitSO _starterKit;

        private SaveCatalog _catalog;

        // Built on first use. Cheap, but there's no reason to build it for a player who never saves.
        private SaveCatalog Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;

                var extras = new List<UnityEngine.Object>();
                extras.AddRange(_startingSpareWeapons);
                extras.AddRange(_startingSpareArmor);
                extras.AddRange(_startingPotions);
                extras.AddRange(_startingScrolls);
                foreach (var hero in _defaultParty)
                {
                    if (hero == null) continue;
                    extras.Add(hero.Weapon);
                    extras.Add(hero.OffHand);
                    extras.Add(hero.Armor);
                    extras.AddRange(hero.BeltPotions);
                    extras.AddRange(hero.BeltScrolls);
                }

                _catalog = SaveCatalog.Build(_themeRegistry, _starterKit, _heroTypes, extras);
                return _catalog;
            }
        }

        // Your hero phase, with nothing walking, attacking or waiting to resolve.
        public bool CanSaveNow()
            => _battle != null
               && _battle.Outcome == BattleOutcome.InProgress
               && _battle.Phase == TurnPhase.Hero
               && !_endTurnPending
               && _movesInProgress == 0
               && !AnyUnitMoving()
               && _swarmEnded.Count == 0
               && _swarmMoves.Count == 0
               && _swarmAfterMoves.Count == 0
               && _swarmAttacks.Count == 0;

        // Hooked up through PauseMenuView.ConfigureSaving. True if the file was written.
        private bool SaveGame()
        {
            if (!CanSaveNow()) return false;

            try
            {
                return SaveService.Write(CaptureSave());
            }
            catch (Exception e)
            {
                Debug.LogError($"BattleController: saving failed: {e}");
                return false;
            }
        }

        // ---- Capture --------------------------------------------------------

        private SaveData CaptureSave()
        {
            var catalog = Catalog;

            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                floor = _currentFloor,
                round = _battle.RoundNumber,
                selectedHeroIndex = Mathf.Max(0, _partyMembers.IndexOf(_battle.CurrentUnit)),
                mapWidth = _map.Width,
                mapHeight = _map.Height
            };

            // The map, x-major: one character per tile.
            var tiles = new StringBuilder(_map.Width * _map.Height);
            var explored = new StringBuilder(_map.Width * _map.Height);
            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var pos = new GridPosition(x, y);
                    tiles.Append((char)('0' + (int)_map.GetTile(pos)));
                    explored.Append(_fog.IsExplored(pos) ? '1' : '0');
                    if (_map.IsDoor(pos) && _map.IsDoorOpen(pos)) data.openDoors.Add(new SaveTile(pos));
                }
            }
            data.tiles = tiles.ToString();
            data.explored = explored.ToString();

            // Heroes (all of them, in party order), then living enemies. Units
            // refer to each other (status sources, memory, trap owners) by
            // position in this combined list.
            var heroes = _partyMembers.ToList();
            var enemies = _unitViews.Keys
                .Where(u => u.Faction == Faction.Enemy && u.IsAlive)
                .Where(u =>
                {
                    if (_enemyKits.ContainsKey(u)) return true;
                    Debug.LogWarning($"BattleController: {u.DisplayName} has no enemy kit, so it isn't saved.");
                    return false;
                })
                .ToList();

            var index = new Dictionary<CombatUnit, int>();
            for (int i = 0; i < heroes.Count; i++) index[heroes[i]] = i;
            for (int i = 0; i < enemies.Count; i++) index[enemies[i]] = heroes.Count + i;

            foreach (var hero in heroes) data.heroes.Add(CaptureUnit(hero, index, catalog));
            foreach (var enemy in enemies) data.enemies.Add(CaptureUnit(enemy, index, catalog));

            foreach (var obj in _battlefieldObjects.Active)
                data.objects.Add(CaptureObject(obj, index));

            foreach (var chest in _chests)
                if (chest != null) data.chests.Add(CaptureContainer(chest, catalog));

            foreach (var pile in _lootOnGround)
                if (!pile.IsEmpty) data.loot.Add(CaptureContainer(pile, catalog));

            for (int i = 0; i < _inventory.SlotCount; i++)
            {
                var slot = _inventory.GetSlot(i);
                data.inventory.Add(slot.IsEmpty ? default(SaveItemStack) : catalog.StackOf(slot.Item, slot.Count));
            }

            return data;
        }

        private SaveUnit CaptureUnit(CombatUnit unit, Dictionary<CombatUnit, int> index, SaveCatalog catalog)
        {
            var stats = unit.Stats;
            var actions = unit.Actions;

            _enemyKits.TryGetValue(unit, out var kit);

            var save = new SaveUnit
            {
                name = unit.DisplayName,
                enemyKit = kit != null ? kit.name : string.Empty,

                body = stats.Body,
                mind = stats.Mind,
                spirit = stats.Spirit,
                level = stats.Level,
                experience = stats.Experience,
                unspentPoints = stats.UnspentPoints,
                consumableBonus = stats.ConsumableBonus,

                mainHand = catalog.NameOf(unit.MainHand),
                offHand = catalog.NameOf(unit.OffHand),
                armor = catalog.NameOf(stats.EquippedArmor),

                position = new SaveTile(unit.Position),
                hp = unit.CurrentHp,
                mp = unit.CurrentMp,
                mpRegenCarry = unit.MpRegenCarry,
                alerted = unit.IsAlerted,

                isOwnersTurn = unit.Status.IsOwnersTurn,

                baseActions = actions.BaseActionsPerPhase,
                actionCarry = actions.Carry,
                actionsRemaining = actions.Remaining,
                actionsBanked = actions.Banked,
                actionsDone = actions.IsDone
            };

            if (unit.RoamZone != null)
            {
                save.hasRoamZone = true;
                foreach (var tile in unit.RoamZone) save.roamZone.Add(new SaveTile(tile));
            }

            foreach (var pair in unit.Cooldowns)
                save.cooldowns.Add(new SaveCooldown { ability = pair.Key.Name, turns = pair.Value });

            foreach (var status in unit.Status.Active)
            {
                int sourceIndex = -1;
                if (status.Source != null && index.TryGetValue(status.Source, out var found)) sourceIndex = found;

                save.statuses.Add(new SaveStatus
                {
                    id = status.Id,
                    stacks = status.Stacks,
                    duration = status.Duration,
                    magnitude = status.Magnitude,
                    statBonus = status.StatBonus,
                    sourceIndex = sourceIndex,
                    primed = status.Primed,
                    skipNextDurationTick = status.SkipNextDurationTick
                });
            }

            for (int slot = 0; slot < unit.Belt.SlotCount; slot++)
                save.belt.Add(catalog.StackOf(unit.Belt.GetItem(slot), unit.Belt.GetCount(slot)));

            foreach (var sighting in unit.Memory.Entries)
            {
                if (!index.TryGetValue(sighting.Target, out var targetIndex)) continue;
                save.memory.Add(new SaveSighting
                {
                    targetIndex = targetIndex,
                    position = new SaveTile(sighting.Position),
                    round = sighting.Round
                });
            }

            return save;
        }

        private SaveBattlefieldObject CaptureObject(BattlefieldObject obj, Dictionary<CombatUnit, int> index)
        {
            int ownerIndex = -1;
            if (obj.OwnerUnit != null && index.TryGetValue(obj.OwnerUnit, out var found)) ownerIndex = found;

            var save = new SaveBattlefieldObject
            {
                name = obj.Name,
                sourceAbility = obj.SourceAbilityName ?? string.Empty,
                ownerFaction = obj.OwnerFaction,
                ownerUnitIndex = ownerIndex,
                remainingDuration = obj.RemainingDuration,
                hasMaxHp = obj.MaxHp.HasValue,
                maxHp = obj.MaxHp ?? 0,
                currentHp = obj.CurrentHp,
                blocksMovement = obj.BlocksMovement,
                blocksLineOfSight = obj.BlocksLineOfSight,
                isHidden = obj.IsHidden,
                triggerMode = obj.TriggerMode,
                auraRadius = obj.AuraRadius,
                affectsAllies = obj.AffectsAllies,
                ignoreOwnerFaction = obj.IgnoreOwnerFaction,
                consumedAfterTrigger = obj.ConsumedAfterTrigger
            };

            if (obj.Tiles != null)
                foreach (var tile in obj.Tiles) save.tiles.Add(new SaveTile(tile));
            if (obj.Statuses != null)
                foreach (var status in obj.Statuses) save.statuses.Add(status);

            return save;
        }

        private static SaveContainer CaptureContainer(ILootContainer container, SaveCatalog catalog)
        {
            var save = new SaveContainer { position = new SaveTile(container.Position) };

            foreach (var weapon in container.Weapons) save.weapons.Add(catalog.NameOf(weapon));
            foreach (var armor in container.Armors) save.armors.Add(catalog.NameOf(armor));
            foreach (var pair in container.Potions) save.consumables.Add(catalog.StackOf(pair.Key, pair.Value));
            foreach (var pair in container.Scrolls) save.consumables.Add(catalog.StackOf(pair.Key, pair.Value));

            return save;
        }

        // ---- Restore --------------------------------------------------------

        // Rebuilds the saved floor and resumes the hero phase. Returns false
        // (having changed nothing) if the save doesn't fit this game, so the
        // caller can start a fresh run instead of showing a broken level.
        private bool TryStartFromSave(SaveData data)
        {
            int expectedTiles = data.mapWidth * data.mapHeight;
            if (data.mapWidth <= 0 || data.mapHeight <= 0
                || data.tiles == null || data.tiles.Length != expectedTiles
                || data.explored == null || data.explored.Length != expectedTiles
                || data.heroes == null || data.heroes.Count == 0)
            {
                Debug.LogError("BattleController: the save doesn't describe a valid floor; starting a new game instead.");
                return false;
            }

            var catalog = Catalog;

            _currentFloor = data.floor;

            // The floor.
            _map = new DungeonMap(data.mapWidth, data.mapHeight);
            int next = 0;
            for (int x = 0; x < data.mapWidth; x++)
                for (int y = 0; y < data.mapHeight; y++)
                    _map.SetTile(new GridPosition(x, y), (TileType)(data.tiles[next++] - '0'));
            foreach (var door in data.openDoors) _map.TryOpenDoor(door.ToGrid());

            _battlefieldObjects = new BattlefieldObjectRegistry();
            _movementMap = new MovementMapView(_map, _battlefieldObjects);
            _doorPassMap = new MovementMapView(_map, _battlefieldObjects, false);
            _sightMap = new SightMapView(_map, _battlefieldObjects);
            _gridView.BuildFrom(_map);

            // The shared inventory, slot for slot.
            _inventory = new PartyInventory();
            for (int i = 0; i < data.inventory.Count; i++)
            {
                var stack = data.inventory[i];
                var item = catalog.Item(stack);
                _inventory.SetSlot(i, item, item != null ? stack.count : 0);
            }
            _inventoryPanel.Initialize(_inventory);
            _inventoryPanel.Hide();

            // Units. `all` lines up with the save's combined index (heroes, then
            // enemies); an enemy that can't be rebuilt leaves a null so the
            // indices that refer to later units stay right.
            var all = new List<CombatUnit>();
            foreach (var saved in data.heroes) all.Add(RestoreHero(saved, catalog));
            foreach (var saved in data.enemies) all.Add(RestoreEnemy(saved, catalog));

            var savedUnits = data.heroes.Concat(data.enemies).ToList();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) RestoreUnitState(all[i], savedUnits[i], all, catalog);

            var heroes = all.Take(data.heroes.Count).ToList();
            var units = all.Where(u => u != null).ToList();

            BindPartyPanels(heroes);
            foreach (var unit in units) SpawnUnitView(unit);

            RestoreChests(data, catalog);
            RestoreLoot(data, catalog);
            RestoreObjects(data, all, catalog);

            // Resume mid-phase: the hero phase has already been set up and the
            // round's object ticks already happened, so BeginTurn must not redo them.
            _heroPhaseRound = data.round;
            _lastTickedRound = data.round;
            _battle = new Battle(units) { EndOnEnemiesDefeated = false };
            var selected = data.selectedHeroIndex >= 0 && data.selectedHeroIndex < heroes.Count
                ? heroes[data.selectedHeroIndex] : null;
            _battle.RestoreHeroPhase(data.round, selected);

            var exploredTiles = new List<GridPosition>();
            next = 0;
            for (int x = 0; x < data.mapWidth; x++)
                for (int y = 0; y < data.mapHeight; y++)
                    if (data.explored[next++] == '1') exploredTiles.Add(new GridPosition(x, y));
            _fog.RestoreExplored(exploredTiles);

            RefreshFogOfWar();

            // Without this the first action would look like "combat just started"
            // and refill every hero's actions (see SyncCombatState).
            _wasInCombat = IsInCombat();

            BeginTurn();
            Debug.Log($"Loaded floor {_currentFloor}, round {data.round}.");
            return true;
        }

        // Stats first (so gear requirements and max HP are right), then gear.
        private CombatUnit RestoreHero(SaveUnit saved, SaveCatalog catalog)
        {
            var stats = new UnitStats(new StatBlock(saved.body, saved.mind, saved.spirit), saved.level);
            stats.RestoreProgress(saved.level, saved.experience, saved.unspentPoints, saved.consumableBonus);

            var hero = new CombatUnit(saved.name, Faction.Player, stats, saved.position.ToGrid(), _playerVisionRadius);
            hero.SetHand(HandSlot.Main, catalog.Weapon(saved.mainHand));
            hero.SetHand(HandSlot.Off, catalog.Weapon(saved.offHand));
            stats.RestoreArmor(catalog.Armor(saved.armor));
            return hero;
        }

        private CombatUnit RestoreEnemy(SaveUnit saved, SaveCatalog catalog)
        {
            var kit = catalog.Enemy(saved.enemyKit);
            if (kit == null)
            {
                Debug.LogWarning($"BattleController: couldn't find enemy '{saved.enemyKit}' from the save, so it was left out.");
                return null;
            }

            var enemy = CreateEnemyUnit(kit, saved.position.ToGrid());
            enemy.Stats.RestoreProgress(saved.level, saved.experience, saved.unspentPoints, saved.consumableBonus);
            return enemy;
        }

        // Everything that can refer to another unit, plus HP/MP (last, because
        // statuses can change the maximums).
        private void RestoreUnitState(CombatUnit unit, SaveUnit saved, List<CombatUnit> all, SaveCatalog catalog)
        {
            if (saved.hasRoamZone) unit.SetRoamZone(saved.roamZone.Select(t => t.ToGrid()));

            foreach (var status in saved.statuses)
            {
                var source = status.sourceIndex >= 0 && status.sourceIndex < all.Count ? all[status.sourceIndex] : null;
                unit.Status.RestoreInstance(status.id, status.stacks, status.duration, status.magnitude,
                    status.statBonus, source, status.primed, status.skipNextDurationTick);
            }
            unit.Status.RestoreOwnersTurn(saved.isOwnersTurn);

            foreach (var cooldown in saved.cooldowns)
            {
                var ability = FindUnitAbility(unit, cooldown.ability) ?? catalog.AbilityByName(cooldown.ability);
                unit.RestoreCooldown(ability, cooldown.turns);
            }

            for (int slot = 0; slot < saved.belt.Count && slot < unit.Belt.SlotCount; slot++)
            {
                var stack = saved.belt[slot];
                var item = catalog.Consumable(stack);
                unit.Belt.SetSlot(slot, item, item != null ? stack.count : 0);
            }

            foreach (var sighting in saved.memory)
            {
                if (sighting.targetIndex < 0 || sighting.targetIndex >= all.Count || all[sighting.targetIndex] == null) continue;
                unit.Memory.Record(all[sighting.targetIndex], sighting.position.ToGrid(), sighting.round);
            }

            unit.Actions.Restore(saved.baseActions, saved.actionCarry, saved.actionsRemaining, saved.actionsBanked, saved.actionsDone);
            unit.RestoreRuntime(saved.hp, saved.mp, saved.mpRegenCarry, saved.alerted);
        }

        // Prefers the exact ability object the unit already has (so a cooldown
        // lands on the right one even if two assets share a display name).
        private static IAbility FindUnitAbility(CombatUnit unit, string abilityName)
        {
            foreach (var ability in unit.WeaponAbilities)
                if (ability != null && ability.Name == abilityName) return ability;

            for (int slot = 0; slot < unit.Belt.SlotCount; slot++)
            {
                var item = unit.Belt.GetItem(slot);
                var ability = item is IScroll scroll ? scroll.Ability
                    : item is IPotion potion ? potion.Ability : null;
                if (ability != null && ability.Name == abilityName) return ability;
            }
            return null;
        }

        private void RestoreChests(SaveData data, SaveCatalog catalog)
        {
            // Chests made in the scene by hand belong to a fresh floor; the save has its own.
            foreach (var old in _chests)
                if (old != null) Destroy(old.gameObject);
            _chests.Clear();

            if (data.chests.Count > 0 && _chestPrefab == null)
            {
                Debug.LogWarning("BattleController: Chest Prefab isn't assigned, so the saved chests can't come back.");
                return;
            }

            foreach (var saved in data.chests)
            {
                var position = saved.position.ToGrid();
                var chest = Instantiate(_chestPrefab, GridToWorld.ToWorldPosition(position), Quaternion.identity, transform);
                chest.Place(position);

                foreach (var name in saved.weapons) chest.AddWeapon(catalog.Weapon(name) as WeaponSO);
                foreach (var name in saved.armors) chest.AddArmor(catalog.Armor(name) as ArmorSO);
                foreach (var stack in saved.consumables)
                {
                    if (stack.kind == SaveItemKind.Potion) chest.AddPotion(catalog.Potion(stack.name) as PotionSO, stack.count);
                    else if (stack.kind == SaveItemKind.Scroll) chest.AddScroll(catalog.Scroll(stack.name) as ScrollSO, stack.count);
                }

                _chests.Add(chest);
            }
        }

        private void RestoreLoot(SaveData data, SaveCatalog catalog)
        {
            _lootOnGround.Clear();

            foreach (var saved in data.loot)
            {
                var position = saved.position.ToGrid();
                var pile = new LootDrop(position, null, null);

                foreach (var name in saved.weapons) pile.AddWeapon(catalog.Weapon(name));
                foreach (var name in saved.armors) pile.AddArmor(catalog.Armor(name));
                foreach (var stack in saved.consumables)
                {
                    if (stack.kind == SaveItemKind.Potion) pile.AddPotion(catalog.Potion(stack.name), stack.count);
                    else if (stack.kind == SaveItemKind.Scroll) pile.AddScroll(catalog.Scroll(stack.name), stack.count);
                }

                if (pile.IsEmpty) continue;
                _lootOnGround.Add(pile);
                SpawnLootMarker(position);
            }
        }

        private void RestoreObjects(SaveData data, List<CombatUnit> all, SaveCatalog catalog)
        {
            foreach (var saved in data.objects)
            {
                var owner = saved.ownerUnitIndex >= 0 && saved.ownerUnitIndex < all.Count ? all[saved.ownerUnitIndex] : null;

                var obj = new BattlefieldObject
                {
                    Name = saved.name,
                    SourceAbilityName = saved.sourceAbility,
                    OwnerFaction = saved.ownerFaction,
                    OwnerUnit = owner,
                    Tiles = saved.tiles.Select(t => t.ToGrid()).ToList(),
                    RemainingDuration = saved.remainingDuration,
                    MaxHp = saved.hasMaxHp ? saved.maxHp : (int?)null,
                    CurrentHp = saved.currentHp,
                    BlocksMovement = saved.blocksMovement,
                    BlocksLineOfSight = saved.blocksLineOfSight,
                    IsHidden = saved.isHidden,
                    TriggerMode = saved.triggerMode,
                    AuraRadius = saved.auraRadius,
                    AffectsAllies = saved.affectsAllies,
                    IgnoreOwnerFaction = saved.ignoreOwnerFaction,
                    ConsumedAfterTrigger = saved.consumedAfterTrigger,
                    Statuses = saved.statuses
                };

                _battlefieldObjects.Add(obj);
                _hazardPrefabs[obj] = (catalog.AbilityByName(saved.sourceAbility) as AbilitySO)?.Visuals?.HazardEffectPrefab;
            }
        }
    }
}
