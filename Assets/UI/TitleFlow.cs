using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// Runs the title scene: the title panel (Play / Quit) and the
    /// character creation panel share this one scene and take turns being
    /// visible. Play opens creation; Start on the creation screen parks
    /// the chosen heroes in PartySetup and loads the game scene.
    /// </summary>
    public sealed class TitleFlow : MonoBehaviour
    {
        [SerializeField] private GameObject _titlePanel;
        [SerializeField] private CharacterCreationView _creation;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _quitButton;
        [Tooltip("Optional. Shown only when a saved game exists; loads it.")]
        [SerializeField] private Button _continueButton;
        [Tooltip("Exact scene name. The scene must be listed in File > Build Profiles (Build Settings).")]
        [SerializeField] private string _gameSceneName = "SampleScene";

        private void Awake()
        {
            // A new run always starts from creation, never from last time's party.
            // RunState matters too now that you can quit to the title mid-run:
            // left over, it would make the next Play carry on the old run.
            PartySetup.Clear();
            RunState.Clear();

            if (_playButton != null) _playButton.onClick.AddListener(OpenCreation);
            if (_quitButton != null) _quitButton.onClick.AddListener(Quit);
            if (_continueButton != null) _continueButton.onClick.AddListener(Continue);

            if (_creation != null)
            {
                _creation.StartRequested += OnStartRequested;
                _creation.BackRequested += ShowTitle;
            }

            ShowTitle();
        }

        private void OnDestroy()
        {
            if (_creation == null) return;
            _creation.StartRequested -= OnStartRequested;
            _creation.BackRequested -= ShowTitle;
        }

        private void ShowTitle()
        {
            RefreshContinueButton();
            if (_titlePanel != null) _titlePanel.SetActive(true);
            if (_creation != null) _creation.Close();
        }

        private void OpenCreation()
        {
            if (_titlePanel != null) _titlePanel.SetActive(false);
            if (_creation != null) _creation.Open();
        }

        private void OnStartRequested(System.Collections.Generic.IReadOnlyList<HeroSetup> heroes)
        {
            if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
            {
                Debug.LogError($"TitleFlow: scene '{_gameSceneName}' can't be loaded — check the name and that it's added in Build Settings.");
                return;
            }

            PartySetup.Set(heroes);
            SceneManager.LoadScene(_gameSceneName);
        }

        private void RefreshContinueButton()
        {
            if (_continueButton != null) _continueButton.gameObject.SetActive(SaveService.Exists());
        }

        // Loads the saved game: the file is read here so a damaged save is
        // caught on the title screen, then handed to the game scene.
        private void Continue()
        {
            if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
            {
                Debug.LogError($"TitleFlow: scene '{_gameSceneName}' can't be loaded — check the name and that it's added in Build Settings.");
                return;
            }

            var save = SaveService.Read();
            if (save == null)
            {
                Debug.LogError("TitleFlow: the saved game couldn't be read (see the messages above).");
                RefreshContinueButton();
                return;
            }

            SaveService.PendingLoad = save;
            SceneManager.LoadScene(_gameSceneName);
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
