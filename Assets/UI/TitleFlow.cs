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
        [Tooltip("Exact scene name. The scene must be listed in File > Build Profiles (Build Settings).")]
        [SerializeField] private string _gameSceneName = "SampleScene";

        private void Awake()
        {
            // A new run always starts from creation, never from last time's party.
            PartySetup.Clear();

            if (_playButton != null) _playButton.onClick.AddListener(OpenCreation);
            if (_quitButton != null) _quitButton.onClick.AddListener(Quit);

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
