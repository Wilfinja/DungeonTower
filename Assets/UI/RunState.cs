using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonTower.Combat;
using DungeonTower.Core;
using DungeonTower.Items;

namespace DungeonTower.UI
{
    /// <summary>
    /// What survives between floors. Descending reloads the scene (which
    /// wipes every tile, unit view, fog layer and loot marker in one go),
    /// so the party's CombatUnits and the shared inventory are parked here
    /// across the reload and picked back up in BattleController.Start().
    ///
    /// This is also the natural hand-off point for the planned title ->
    /// character creation -> game flow: character creation can fill Party
    /// and Inventory here before loading the game scene.
    /// </summary>
    public static class RunState
    {
        public static bool IsActive { get; private set; }
        public static int Floor { get; private set; } = 1;
        public static List<CombatUnit> Party { get; private set; } = new List<CombatUnit>();
        public static PartyInventory Inventory { get; private set; }

        public static void CarryToNextFloor(int nextFloor, IEnumerable<CombatUnit> party, PartyInventory inventory)
        {
            IsActive = true;
            Floor = nextFloor;
            Party = party.ToList();
            Inventory = inventory;
        }

        // Call when the run ends (party wiped) so the next Play starts fresh.
        public static void Clear()
        {
            IsActive = false;
            Floor = 1;
            Party = new List<CombatUnit>();
            Inventory = null;
        }

        // Statics survive Play Mode if "Reload Domain" is turned off in
        // Project Settings > Editor, which would leak a run into the next
        // Play. This guarantees every Play starts clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
