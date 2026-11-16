using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// The in-game pause menu: Resume, Save, Options, Save and Quit to
    /// Title, Abandon Run and Quit to Desktop. Escape opens and closes it.
    ///
    /// Escape belongs to the game first: if it just backed out of aiming,
    /// dismissed a tooltip or cleared a path preview, BattleController
    /// calls MarkEscapeHandled() and the menu stays shut that frame. This
    /// checks in LateUpdate, after every Update has had its say.
    ///
    /// Put this component on an object that is ALWAYS active (it needs
    /// LateUpdate to hear Escape). _overlay is a child that holds the
    /// dimmer and the panels; it starts inactive and is what gets shown.
    ///
    /// Saving is optional. Until something calls ConfigureSaving the Save
    /// buttons stay hidden, so the menu works fine without it.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Structure")]
        [Tooltip("Child that holds the dimmer + panels. Starts inactive.")]
        [SerializeField] private GameObject _overlay;
        [Tooltip("The panel with the main buttons. Hidden while Options is showing.")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private OptionsView _options;
        [SerializeField] private ConfirmDialogView _confirm;

        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _saveAndQuitButton;
        [SerializeField] private Button _abandonButton;
        [SerializeField] private Button _quitDesktopButton;

        [Tooltip("Optional. Shows 'Game saved.' or why saving isn't allowed right now.")]
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Tooltip("Exact name of the title scene (must be in Build Settings).")]
        [SerializeField] private string _titleSceneName = "Title";

        private static int _escapeHandledFrame = -1;

        private Func<bool> _canSave;
        private Func<bool> _save;

        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        /// <summary>Called by gameplay code when it used this frame's Escape press.</summary>
        public static void MarkEscapeHandled() => _escapeHandledFrame = Time.frameCount;

        /// <summary>
        /// Turns the Save buttons on. canSave says whether the game is in a
        /// state that can be saved right now; save does it and returns true
        /// if it worked.
        /// </summary>
        public void ConfigureSaving(Func<bool> canSave, Func<bool> save)
        {
            _canSave = canSave;
            _save = save;
            RefreshSaveButtons();
        }

        private void Awake()
        {
            if (_overlay != null) _overlay.SetActive(false);
            if (_confirm != null) _confirm.Hide();
            if (_options != null)
            {
                _options.Hide();
                _options.BackRequested += CloseOptions;
            }

            if (_resumeButton != null) _resumeButton.onClick.AddListener(Close);
            if (_saveButton != null) _saveButton.onClick.AddListener(SaveClicked);
            if (_optionsButton != null) _optionsButton.onClick.AddListener(OpenOptions);
            if (_saveAndQuitButton != null) _saveAndQuitButton.onClick.AddListener(SaveAndQuitClicked);
            if (_abandonButton != null)
                _abandonButton.onClick.AddListener(() => Ask(
                    "Abandon this run?\nYour progress will be lost.", AbandonRun));
            if (_quitDesktopButton != null)
                _quitDesktopButton.onClick.AddListener(() => Ask(
                    "Quit to desktop?\nProgress since your last save will be lost.", QuitToDesktop));

            RefreshSaveButtons();
        }

        private void OnDestroy()
        {
            if (_options != null) _options.BackRequested -= CloseOptions;
            if (IsOpen) GamePause.Set(false);   // never carry a pause into the next scene
        }

        // LateUpdate so every Update (aim cancel, tooltip dismiss) has run
        // and had its chance to claim this frame's Escape.
        private void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (!IsOpen)
            {
                if (_escapeHandledFrame != Time.frameCount) Open();
                return;
            }

            // Back out one layer at a time.
            if (_confirm != null && _confirm.IsOpen) _confirm.Hide();
            else if (_options != null && _options.IsOpen) CloseOptions();
            else Close();
        }

        public void Open()
        {
            if (_overlay == null || IsOpen) return;
            _overlay.SetActive(true);
            ShowMain();
            if (_statusLabel != null) _statusLabel.text = string.Empty;
            RefreshSaveButtons();
            GamePause.Set(true);
        }

        public void Close()
        {
            if (_overlay == null || !IsOpen) return;
            if (_confirm != null) _confirm.Hide();
            if (_options != null) _options.Hide();
            _overlay.SetActive(false);
            GamePause.Set(false);
        }

        private void ShowMain()
        {
            if (_options != null) _options.Hide();
            if (_mainPanel != null) _mainPanel.SetActive(true);
        }

        private void OpenOptions()
        {
            if (_options == null) return;
            if (_mainPanel != null) _mainPanel.SetActive(false);
            _options.Show();
        }

        private void CloseOptions() => ShowMain();

        private void Ask(string message, Action onYes)
        {
            if (_confirm != null) _confirm.Show(message, onYes);
            else onYes?.Invoke();   // no dialog wired: don't make the button dead
        }

        // ---- Saving -----------------------------------------------------

        private void RefreshSaveButtons()
        {
            bool enabled = _save != null;
            if (_saveButton != null) _saveButton.gameObject.SetActive(enabled);
            if (_saveAndQuitButton != null) _saveAndQuitButton.gameObject.SetActive(enabled);
        }

        private bool TrySave()
        {
            if (_save == null) return false;
            if (_canSave != null && !_canSave())
            {
                SetStatus("Can't save in the middle of an action. Try again in a moment.");
                return false;
            }

            bool ok = _save();
            SetStatus(ok ? "Game saved." : "Save failed. Check the Console for details.");
            return ok;
        }

        private void SaveClicked() => TrySave();

        private void SaveAndQuitClicked()
        {
            if (TrySave()) GoToTitle();
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        // ---- Leaving the game ---------------------------------------------

        private void AbandonRun()
        {
            if (!CanLoadTitle()) return;
            RunState.Clear();
            SaveService.Delete();   // the run is over, so there's nothing to continue
            GoToTitle();
        }

        private bool CanLoadTitle()
        {
            if (Application.CanStreamedLevelBeLoaded(_titleSceneName)) return true;
            Debug.LogError($"PauseMenuView: scene '{_titleSceneName}' can't be loaded; check the name and Build Settings.");
            SetStatus("Can't reach the title screen. Check the Console.");
            return false;
        }

        private void GoToTitle()
        {
            if (!CanLoadTitle()) return;
            GamePause.Set(false);
            SceneManager.LoadScene(_titleSceneName);
        }

        private static void QuitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
