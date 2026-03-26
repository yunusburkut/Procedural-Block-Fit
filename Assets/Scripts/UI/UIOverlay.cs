
using UnityEngine;
using UnityEngine.UI;

namespace Blokfit.UI
{
    public class UIOverlay : MonoBehaviour
    {
        [SerializeField] private Button          _restartButton;
        [SerializeField] private GameObject      _completionPanel;

        private System.Action _onRestartRequested;

        private void Awake()
        {
            _restartButton.onClick.AddListener(HandleRestartClicked);
            HideCompletion();
        }

        private void OnDestroy()
        {
            _restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        public void SetRestartCallback(System.Action callback)
        {
            _onRestartRequested = callback;
        }

        public void UpdateMoveCounter(int count)
        {
        }

        public void ShowCompletion()
        {
            if (_completionPanel != null)
                _completionPanel.SetActive(true);
        }

        public void HideCompletion()
        {
            if (_completionPanel != null)
                _completionPanel.SetActive(false);
        }

        private void HandleRestartClicked()
        {
            _onRestartRequested?.Invoke();
        }
    }
}
