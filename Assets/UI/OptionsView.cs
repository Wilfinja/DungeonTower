using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// The options screen: three volume sliders, a screen shake toggle and
    /// a fast combat toggle, all reading and writing GameSettings (so they
    /// save themselves). Put this on the options panel, which starts
    /// inactive. It works anywhere — the pause menu opens it today, and a
    /// title-screen Options button could open it the same way.
    ///
    /// Every control is optional, so you can leave out any you don't want.
    /// </summary>
    public sealed class OptionsView : MonoBehaviour
    {
        [Header("Volume (sliders should run 0 to 1)")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [Tooltip("Optional. Shows 0-100% next to each slider.")]
        [SerializeField] private TextMeshProUGUI _masterLabel;
        [SerializeField] private TextMeshProUGUI _musicLabel;
        [SerializeField] private TextMeshProUGUI _sfxLabel;

        [Header("Toggles")]
        [SerializeField] private Toggle _screenShakeToggle;
        [SerializeField] private Toggle _fastCombatToggle;

        [SerializeField] private Button _backButton;

        /// <summary>Raised when Back is pressed; the owner decides what to show next.</summary>
        public event Action BackRequested;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            Wire(_masterSlider, v => GameSettings.MasterVolume = v);
            Wire(_musicSlider, v => GameSettings.MusicVolume = v);
            Wire(_sfxSlider, v => GameSettings.SfxVolume = v);

            if (_screenShakeToggle != null)
                _screenShakeToggle.onValueChanged.AddListener(on => GameSettings.ScreenShake = on);
            if (_fastCombatToggle != null)
                _fastCombatToggle.onValueChanged.AddListener(on => GameSettings.FastCombat = on);
            if (_backButton != null)
                _backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        private void OnEnable() => LoadFromSettings();

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        private void Wire(Slider slider, Action<float> apply)
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(v =>
            {
                apply(v);
                UpdateLabels();
            });
        }

        // SetValueWithoutNotify so opening the screen doesn't rewrite the
        // settings it just read.
        private void LoadFromSettings()
        {
            if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            if (_screenShakeToggle != null) _screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShake);
            if (_fastCombatToggle != null) _fastCombatToggle.SetIsOnWithoutNotify(GameSettings.FastCombat);
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            SetPercent(_masterLabel, GameSettings.MasterVolume);
            SetPercent(_musicLabel, GameSettings.MusicVolume);
            SetPercent(_sfxLabel, GameSettings.SfxVolume);
        }

        private static void SetPercent(TextMeshProUGUI label, float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
