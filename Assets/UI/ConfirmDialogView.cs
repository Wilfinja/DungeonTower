using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// A small Yes / No box for actions you can't undo. Call
    /// Show("message", onYes); No (or Escape, handled by whoever owns the
    /// dialog) just closes it. Put this on the dialog's root panel, which
    /// starts inactive.
    /// </summary>
    public sealed class ConfirmDialogView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageLabel;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;

        private Action _onYes;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_yesButton != null) _yesButton.onClick.AddListener(Confirm);
            if (_noButton != null) _noButton.onClick.AddListener(Hide);
        }

        public void Show(string message, Action onYes)
        {
            _onYes = onYes;
            if (_messageLabel != null) _messageLabel.text = message;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _onYes = null;
            gameObject.SetActive(false);
        }

        private void Confirm()
        {
            var action = _onYes;
            Hide();
            action?.Invoke();
        }
    }
}
