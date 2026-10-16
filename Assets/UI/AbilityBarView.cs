using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// On-screen buttons for the current player unit's turn options: an
    /// always-available Move button plus up to four weapon-ability
    /// buttons. Purely a view — BattleController owns which mode is
    /// actually active and what selecting one means for the turn, and
    /// tells this view whether each ability is on cooldown right now
    /// (via the remainingCooldown callback passed to Show) rather than
    /// this view knowing anything about CombatUnit itself.
    /// AbilitySelected fires with the clicked ability button's index (0-3); MoveSelected fires separately since Move isn't an ability.
    ///
    /// NOTE: this adds a new Move button/label pair not present before —
    /// wire up a Button + TextMeshProUGUI in the prefab/scene and assign
    /// them to _moveButton/_moveLabel.
    /// </summary>
    public sealed class AbilityBarView : MonoBehaviour
    {
        [SerializeField] private Button _moveButton;
        [SerializeField] private TextMeshProUGUI _moveLabel;
        [SerializeField] private Button _waitButton;
        public event Action WaitSelected;
        [SerializeField] private Button _button1;
        [SerializeField] private Button _button2;
        [SerializeField] private TextMeshProUGUI _label1;
        [SerializeField] private TextMeshProUGUI _label2;
        [Tooltip("Optional until a build has 3+ abilities: with both hands full a hero can have 4.")]
        [SerializeField] private Button _button3;
        [SerializeField] private Button _button4;
        [SerializeField] private TextMeshProUGUI _label3;
        [SerializeField] private TextMeshProUGUI _label4;

        private Button[] _buttons;
        private TextMeshProUGUI[] _labels;
        private bool _warnedMissing;

        // Built on first use so it works whether or not Awake has run yet.
        private void EnsureArrays()
        {
            if (_buttons != null) return;
            _buttons = new[] { _button1, _button2, _button3, _button4 };
            _labels = new[] { _label1, _label2, _label3, _label4 };
        }

        public event Action<int> AbilitySelected;
        public event Action MoveSelected;

        private void Awake()
        {
            _moveButton.onClick.AddListener(() => MoveSelected?.Invoke());
            EnsureArrays();
            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                if (_buttons[i] != null) _buttons[i].onClick.AddListener(() => AbilitySelected?.Invoke(index));
            }
            if (_waitButton != null) _waitButton.onClick.AddListener(() => WaitSelected?.Invoke());
        }

        // remainingCooldown(ability) should return 0 for "ready", or the
        // number of the unit's own turns left before it's usable again.
        public void Show(IReadOnlyList<IAbility> abilities, Func<IAbility, int> remainingCooldown, Func<IAbility, bool> isBlocked = null)
        {
            EnsureArrays();
            gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);
            if (_waitButton != null) _waitButton.gameObject.SetActive(true);

            for (int i = 0; i < _buttons.Length; i++)
            {
                var button = _buttons[i];
                bool has = abilities != null && i < abilities.Count;

                if (button == null)
                {
                    if (has && !_warnedMissing)
                    {
                        _warnedMissing = true;
                        Debug.LogWarning($"AbilityBarView has no button {i + 1} assigned, so ability '{abilities[i].Name}' can't be shown.", this);
                    }
                    continue;
                }

                button.gameObject.SetActive(has);
                if (has && _labels[i] != null) SetAbilityButton(button, _labels[i], abilities[i], remainingCooldown, isBlocked);
            }
        }

        private static void SetAbilityButton(Button button, TextMeshProUGUI label, IAbility ability,
            Func<IAbility, int> remainingCooldown, Func<IAbility, bool> isBlocked)
        {
            int remaining = remainingCooldown != null ? remainingCooldown(ability) : 0;
            bool onCooldown = remaining > 0;
            bool blocked = isBlocked != null && isBlocked(ability);

            button.interactable = !onCooldown && !blocked;
            label.text = blocked ? $"{ability.Name} (silenced)"
                : onCooldown ? $"{ability.Name} ({remaining})"
                : ability.Name;
        }

        // Weaponless units have nothing to swing, but they can still
        // walk around — Move stays available on its own.
        public void ShowMoveOnly()
        {
            gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);
            EnsureArrays();
            foreach (var button in _buttons)
                if (button != null) button.gameObject.SetActive(false);
            if (_waitButton != null) _waitButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Selection is mutually exclusive across Move/Ability1/Ability2.
        // Pass a negative index to mean "Move is selected." Doesn't
        // touch label text — Show already set that, cooldown suffix
        // included — only the bold/normal style.
        public void SetSelected(int index)
        {
            _moveLabel.fontStyle = index < 0 ? FontStyles.Bold : FontStyles.Normal;
            EnsureArrays();
            for (int i = 0; i < _labels.Length; i++)
                if (_labels[i] != null) _labels[i].fontStyle = index == i ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
