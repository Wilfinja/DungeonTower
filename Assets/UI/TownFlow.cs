using System;
using System.Collections.Generic;
using DungeonTower.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// Runs the town scene, the hub you return to between runs (it replaces
    /// the old title screen). The main town panel has a button per building,
    /// plus Descend (start a new run with the party picked at the tavern),
    /// Resume Run (when a run save exists) and Quit. Each building opens its
    /// own panel; Back buttons return to the town panel.
    ///
    /// A building whose panel isn't assigned yet shows a disabled button,
    /// so buildings can be added one at a time.
    /// </summary>
    public sealed class TownFlow : MonoBehaviour
    {
        [Serializable]
        public sealed class Building
        {
            public string Name;
            public Button Button;
            [Tooltip("The panel opened by the button. Leave empty until the building exists.")]
            public GameObject Panel;
        }

        [Header("Screens")]
        [SerializeField] private GameObject _townPanel;
        [SerializeField] private List<Building> _buildings = new List<Building>();
        [Tooltip("Every Back button on every building panel.")]
        [SerializeField] private List<Button> _backButtons = new List<Button>();

        [Header("Tavern (needed to start a run)")]
        [SerializeField] private TavernView _tavern;

        [Header("Run")]
        [SerializeField] private Button _descendButton;
        [Tooltip("Optional. Shown only when a saved run exists.")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _quitButton;
        [Tooltip("Optional. Asks before a new run replaces a saved one.")]
        [SerializeField] private ConfirmDialogView _confirm;
        [Tooltip("Exact scene name. The scene must be listed in File > Build Profiles (Build Settings).")]
        [SerializeField] private string _gameSceneName = "Dungeon";

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _gemsLabel;
        [Tooltip("Optional. Says why Descend didn't start.")]
        [SerializeField] private TextMeshProUGUI _messageLabel;

        private void Awake()
        {
            // The town is where every run starts, so nothing from a previous run may linger.
            PartySetup.Clear();
            RunState.Clear();
            SaveService.PendingLoad = null;

            foreach (var building in _buildings)
            {
                if (building.Button == null) continue;
                var captured = building;
                building.Button.interactable = building.Panel != null;
                building.Button.onClick.AddListener(() => ShowBuilding(captured));
            }

            foreach (var back in _backButtons)
                if (back != null) back.onClick.AddListener(ShowTown);

            if (_descendButton != null) _descendButton.onClick.AddListener(OnDescendPressed);
            if (_resumeButton != null) _resumeButton.onClick.AddListener(ResumeRun);
            if (_quitButton != null) _quitButton.onClick.AddListener(Quit);

            ShowTown();
        }

        // ---- Screens ----------------------------------------------------------

        public void ShowTown()
        {
            foreach (var building in _buildings)
                if (building.Panel != null) building.Panel.SetActive(false);
            if (_townPanel != null) _townPanel.SetActive(true);

            if (_resumeButton != null) _resumeButton.gameObject.SetActive(SaveService.Exists());
            RefreshInfo();
            Say(string.Empty);
        }

        private void ShowBuilding(Building building)
        {
            if (building.Panel == null) return;
            if (_townPanel != null) _townPanel.SetActive(false);
            foreach (var other in _buildings)
                if (other.Panel != null) other.Panel.SetActive(other == building);
        }

        private void RefreshInfo()
        {
            if (_gemsLabel != null) _gemsLabel.text = $"Gems: {ProfileService.Gems}";
        }

        // ---- Starting a run ---------------------------------------------------

        private void OnDescendPressed()
        {
            if (_tavern == null || !_tavern.PartyReady)
            {
                Say($"Pick {TavernView.PartySize} heroes at the tavern first.");
                return;
            }

            if (SaveService.Exists() && _confirm != null)
                _confirm.Show("Start a new run?\nYour saved run will be replaced.", BeginNewRun);
            else
                BeginNewRun();
        }

        private void BeginNewRun()
        {
            if (!CanLoadGameScene()) return;

            // Two heroes of the same type get numbered so they can be told apart.
            var setups = new List<HeroSetup>();
            var seen = new Dictionary<string, int>();
            foreach (var type in _tavern.Party)
            {
                var setup = type.ToSetup();
                seen.TryGetValue(setup.Name, out var count);
                seen[setup.Name] = count + 1;
                if (count > 0) setup.Name += " " + (count + 1);
                setups.Add(setup);
            }

            SaveService.Delete();
            RunState.Clear();
            PartySetup.Set(setups);
            SceneManager.LoadScene(_gameSceneName);
        }

        // Loads the saved run: the file is read here so a damaged save is
        // caught in town, then handed to the dungeon scene.
        private void ResumeRun()
        {
            if (!CanLoadGameScene()) return;

            var save = SaveService.Read();
            if (save == null)
            {
                Debug.LogError("TownFlow: the saved run couldn't be read (see the messages above).");
                Say("The saved run can't be loaded.");
                ShowTown();
                return;
            }

            SaveService.PendingLoad = save;
            SceneManager.LoadScene(_gameSceneName);
        }

        private bool CanLoadGameScene()
        {
            if (Application.CanStreamedLevelBeLoaded(_gameSceneName)) return true;
            Debug.LogError($"TownFlow: scene '{_gameSceneName}' can't be loaded — check the name and that it's added in Build Settings.");
            Say("The dungeon scene can't be loaded. Check the Console.");
            return false;
        }

        private void Say(string text)
        {
            if (_messageLabel != null) _messageLabel.text = text;
        }

        // Right-click this component's header in Play mode to try the tavern without playing a run.
        [ContextMenu("Add 100 gems (debug)")]
        private void DebugAddGems()
        {
            ProfileService.AddGems(100);
            RefreshInfo();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
