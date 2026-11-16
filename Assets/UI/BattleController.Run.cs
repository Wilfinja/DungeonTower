using UnityEngine;
using DungeonTower.Combat;
using DungeonTower.Core;
using DungeonTower.Items;

namespace DungeonTower.UI
{
    /// <summary>
    /// How a run ties into the town: gems earned in the dungeon, and what
    /// happens when the party is wiped. Heroes now come from the hero types
    /// picked at the tavern (see TownFlow), so a run that starts from the
    /// town needs nothing set up here beyond the fields below.
    /// </summary>
    public sealed partial class BattleController
    {
        [Header("Town")]
        [Tooltip("Every hero type in the game. Lets a loaded save find the heroes' starting gear again.")]
        [SerializeField] private HeroTypeRegistrySO _heroTypes;
        [Tooltip("Shown when the whole party falls. Starts inactive.")]
        [SerializeField] private RunEndView _runEnd;

        [Header("Gems (placeholder numbers, tune freely)")]
        [Tooltip("Gems earned each time the party descends to the next floor.")]
        [SerializeField, Min(0)] private int _gemsPerFloorCleared = 5;
        [Tooltip("Gems earned for each boss killed.")]
        [SerializeField, Min(0)] private int _gemsPerBossKill = 10;

        // Gems go straight into the town profile, so they're kept whether the
        // run is won, lost or abandoned.
        private void AwardFloorGems() => ProfileService.AddGems(_gemsPerFloorCleared);

        private void AwardBossGems() => ProfileService.AddGems(_gemsPerBossKill);

        // The whole party has fallen: the run is over for good, so its save goes
        // too. The town (gems, unlocks, upgrades) is untouched.
        private void OnRunLost()
        {
            RunState.Clear();
            SaveService.Delete();
            if (_runEnd != null)
                _runEnd.Show("Your party has fallen", "The run is over. Your gems and upgrades are safe in town.");
            else
                Debug.LogWarning("BattleController: Run End isn't assigned, so there's no way back to town after a defeat.");
        }
    }
}
