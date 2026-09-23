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
    /// actually active and what selecting one means for the turn.
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

        public void Show(IWeapon weapon)
        {
            gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);

            bool hasFirst = weapon != null && weapon.Abilities.Count > 0;
            bool hasSecond = weapon != null && weapon.Abilities.Count > 1;

            _button1.gameObject.SetActive(hasFirst);
            if (hasFirst) _label1.text = weapon.Abilities[0].Name;

            _button2.gameObject.SetActive(hasSecond);
            if (hasSecond) _label2.text = weapon.Abilities[1].Name;
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
        // Pass a negative index to mean "Move is selected."
        public void SetSelected(int index)
        {
            _moveLabel.fontStyle = index < 0 ? FontStyles.Bold : FontStyles.Normal;
            _label1.fontStyle = index == 0 ? FontStyles.Bold : FontStyles.Normal;
            _label2.fontStyle = index == 1 ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
