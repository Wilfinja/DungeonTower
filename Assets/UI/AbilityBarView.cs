using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// On-screen buttons for the current player unit's turn options: an
    /// always-available Move button plus up to two weapon-ability
    /// buttons. Purely a view — BattleController owns which mode is
    /// actually active and what selecting one means for the turn, and
    /// tells this view whether each ability is on cooldown right now
    /// (via the remainingCooldown callback passed to Show) rather than
    /// this view knowing anything about CombatUnit itself.
    /// AbilitySelected fires with the clicked ability button's index (0
    /// or 1); MoveSelected fires separately since Move isn't an ability.
    ///
    /// NOTE: this adds a new Move button/label pair not present before —
    /// wire up a Button + TextMeshProUGUI in the prefab/scene and assign
    /// them to _moveButton/_moveLabel.
    /// </summary>
    public sealed class AbilityBarView : MonoBehaviour
    {
        [SerializeField] private Button _moveButton;
        [SerializeField] private TextMeshProUGUI _moveLabel;
        [SerializeField] private Button _button1;
        [SerializeField] private Button _button2;
        [SerializeField] private TextMeshProUGUI _label1;
        [SerializeField] private TextMeshProUGUI _label2;

        public event Action<int> AbilitySelected;
        public event Action MoveSelected;

        private void Awake()
        {
            _moveButton.onClick.AddListener(() => MoveSelected?.Invoke());
            _button1.onClick.AddListener(() => AbilitySelected?.Invoke(0));
            _button2.onClick.AddListener(() => AbilitySelected?.Invoke(1));
        }

        // remainingCooldown(ability) should return 0 for "ready", or the
        // number of the unit's own turns left before it's usable again.
        public void Show(IWeapon weapon, Func<IAbility, int> remainingCooldown)
        {
            gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);

            bool hasFirst = weapon != null && weapon.Abilities.Count > 0;
            bool hasSecond = weapon != null && weapon.Abilities.Count > 1;

            _button1.gameObject.SetActive(hasFirst);
            if (hasFirst) SetAbilityButton(_button1, _label1, weapon.Abilities[0], remainingCooldown);

            _button2.gameObject.SetActive(hasSecond);
            if (hasSecond) SetAbilityButton(_button2, _label2, weapon.Abilities[1], remainingCooldown);
        }

        private static void SetAbilityButton(Button button, TextMeshProUGUI label, IAbility ability, Func<IAbility, int> remainingCooldown)
        {
            int remaining = remainingCooldown != null ? remainingCooldown(ability) : 0;
            bool onCooldown = remaining > 0;
            button.interactable = !onCooldown;
            label.text = onCooldown ? $"{ability.Name} ({remaining})" : ability.Name;
        }

        // Weaponless units have nothing to swing, but they can still
        // walk around — Move stays available on its own.
        public void ShowMoveOnly()
        {
            gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);
            _button1.gameObject.SetActive(false);
            _button2.gameObject.SetActive(false);
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
            _label1.fontStyle = index == 0 ? FontStyles.Bold : FontStyles.Normal;
            _label2.fontStyle = index == 1 ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
