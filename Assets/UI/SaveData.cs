using System;
using System.Collections.Generic;
using DungeonTower.Combat;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    // Plain data that JsonUtility can write and read. Nothing here knows how
    // to build the game; BattleController.Save.cs fills these in and reads
    // them back. Items and enemies are stored by their asset NAME and found
    // again through SaveCatalog, so renaming an asset breaks old saves.
    //
    // JsonUtility can't store null lists or strings, so "nothing" is an
    // empty list / empty string throughout.

    [Serializable]
    public sealed class SaveData
    {
        // Bump when the layout changes in a way old saves can't survive.
        public const int CurrentVersion = 1;

        public int version;
        public string savedAtUtc;

        public int floor;
        public int round;
        public int selectedHeroIndex;

        // The floor's map. tiles is one character per tile ('0'-'3', the
        // TileType value), x-major to match DungeonMap. explored is a
        // '0'/'1' string in the same order.
        public int mapWidth;
        public int mapHeight;
        public string tiles;
        public string explored;
        public List<SaveTile> openDoors = new List<SaveTile>();

        // Heroes first (every party member, dead ones included so the party
        // order survives), then the enemies still alive.
        public List<SaveUnit> heroes = new List<SaveUnit>();
        public List<SaveUnit> enemies = new List<SaveUnit>();

        public List<SaveBattlefieldObject> objects = new List<SaveBattlefieldObject>();
        public List<SaveContainer> chests = new List<SaveContainer>();
        public List<SaveContainer> loot = new List<SaveContainer>();

        // One entry per inventory slot, empty ones included, so the
        // arrangement the player made is kept.
        public List<SaveItemStack> inventory = new List<SaveItemStack>();
    }

    [Serializable]
    public struct SaveTile
    {
        public int x;
        public int y;

        public SaveTile(GridPosition position) { x = position.X; y = position.Y; }
        public GridPosition ToGrid() => new GridPosition(x, y);
    }

    public enum SaveItemKind { None = 0, Weapon = 1, Armor = 2, Potion = 3, Scroll = 4 }

    [Serializable]
    public struct SaveItemStack
    {
        public SaveItemKind kind;
        public string name;
        public int count;
    }

    [Serializable]
    public sealed class SaveContainer
    {
        public SaveTile position;
        public List<string> weapons = new List<string>();
        public List<string> armors = new List<string>();
        public List<SaveItemStack> consumables = new List<SaveItemStack>();   // potions and scrolls
    }

    [Serializable]
    public sealed class SaveUnit
    {
        public string name;
        public string enemyKit;          // EnemySO asset name; empty for heroes

        public int body;
        public int mind;
        public int spirit;
        public int level;
        public int experience;
        public int unspentPoints;
        public DerivedStatBonus consumableBonus;

        public string mainHand;
        public string offHand;
        public string armor;

        public SaveTile position;
        public int hp;
        public int mp;
        public float mpRegenCarry;
        public bool alerted;

        public bool hasRoamZone;
        public List<SaveTile> roamZone = new List<SaveTile>();

        public List<SaveCooldown> cooldowns = new List<SaveCooldown>();
        public List<SaveStatus> statuses = new List<SaveStatus>();
        public bool isOwnersTurn;

        public int baseActions;
        public int actionCarry;
        public int actionsRemaining;
        public int actionsBanked;
        public bool actionsDone;

        public List<SaveItemStack> belt = new List<SaveItemStack>();   // one entry per belt slot
        public List<SaveSighting> memory = new List<SaveSighting>();
    }

    [Serializable]
    public struct SaveCooldown
    {
        public string ability;
        public int turns;
    }

    [Serializable]
    public struct SaveStatus
    {
        public StatusEffectId id;
        public int stacks;
        public int duration;
        public float magnitude;
        public DerivedStatBonus statBonus;
        public int sourceIndex;          // index into heroes + enemies; -1 for none
        public bool primed;
        public bool skipNextDurationTick;
    }

    [Serializable]
    public struct SaveSighting
    {
        public int targetIndex;
        public SaveTile position;
        public int round;
    }

    [Serializable]
    public sealed class SaveBattlefieldObject
    {
        public string name;
        public string sourceAbility;
        public Faction ownerFaction;
        public int ownerUnitIndex;       // -1 for none
        public List<SaveTile> tiles = new List<SaveTile>();
        public int remainingDuration;
        public bool hasMaxHp;
        public int maxHp;
        public int currentHp;
        public bool blocksMovement;
        public bool blocksLineOfSight;
        public bool isHidden;
        public SummonTriggerMode triggerMode;
        public int auraRadius;
        public bool affectsAllies;
        public bool ignoreOwnerFaction;
        public bool consumedAfterTrigger;
        public List<StatusApplication> statuses = new List<StatusApplication>();
    }
}
