using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonTower.UI
{
    /// <summary>
    /// The screen shown when a run ends (so far, only when the whole party
    /// falls): a message and a Return to Town button. Put this on a panel
    /// that starts inactive; BattleController calls Show().
    /// </summary>
    public sealed class RunEndView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleLabel;
        [SerializeField] private TextMeshProUGUI _detailLabel;
        [SerializeField] private Button _returnButton;
        [Tooltip("Exact name of the town scene (must be in Build Settings).")]
        [SerializeField] private string _townSceneName = "Town";

        private void Awake()
        {
            if (_returnButton != null) _returnButton.onClick.AddListener(ReturnToTown);
        }

        public void Show(string title, string detail)
        {
            if (_titleLabel != null) _titleLabel.text = title;
            if (_detailLabel != null) _detailLabel.text = detail;
            gameObject.SetActive(true);
        }

        private void ReturnToTown()
        {
            if (!Application.CanStreamedLevelBeLoaded(_townSceneName))
            {
                Debug.LogError($"RunEndView: scene '{_townSceneName}' can't be loaded; check the name and Build Settings.");
                return;
            }
            SceneManager.LoadScene(_townSceneName);
        }
    }
}
