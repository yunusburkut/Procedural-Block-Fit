
using UnityEngine;
using UnityEngine.UI;

namespace Blokfit.UI
{
    public class UIOverlay : MonoBehaviour
    {
        [SerializeField] private Button     _restartButton;
        [SerializeField] private Button     _easyButton;
        [SerializeField] private Button     _mediumButton;
        [SerializeField] private Button     _hardButton;
        [SerializeField] private GameObject _completionPanel;

        private System.Action _onRestartRequested;
        private System.Action _onEasy;
        private System.Action _onMedium;
        private System.Action _onHard;

        private void Awake()
        {
            if (_restartButton != null) _restartButton.onClick.AddListener(() => _onRestartRequested?.Invoke());
            if (_easyButton   != null) _easyButton  .onClick.AddListener(() => _onEasy?.Invoke());
            if (_mediumButton != null) _mediumButton .onClick.AddListener(() => _onMedium?.Invoke());
            if (_hardButton   != null) _hardButton  .onClick.AddListener(() => _onHard?.Invoke());

            HideCompletion();
        }

        public void SetRestartCallback(System.Action callback)   => _onRestartRequested = callback;
        public void SetDifficultyCallbacks(System.Action easy, System.Action medium, System.Action hard)
        {
            _onEasy   = easy;
            _onMedium = medium;
            _onHard   = hard;
        }

        public void UpdateMoveCounter(int count) { }

        public void ShowCompletion()
        {
            if (_completionPanel != null) _completionPanel.SetActive(true);
        }

        public void HideCompletion()
        {
            if (_completionPanel != null) _completionPanel.SetActive(false);
        }
    }
}
