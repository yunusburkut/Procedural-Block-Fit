
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

namespace Blokfit.UI
{
    public class UIOverlay : MonoBehaviour
    {
        [SerializeField] private Button     _easyButton;
        [SerializeField] private Button     _mediumButton;
        [SerializeField] private Button     _hardButton;
        [SerializeField] private GameObject _completionPanel;
        [SerializeField] private Button     _nextLevelButton;
        [SerializeField] private TMP_Text   _doneText;
        [SerializeField] private TMP_Text   _tapToContinueText;

        private System.Action _onNextLevel;
        private System.Action _onEasy;
        private System.Action _onMedium;
        private System.Action _onHard;

        private void Awake()
        {
            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(() => _onNextLevel?.Invoke());
            if (_easyButton     != null) _easyButton    .onClick.AddListener(() => _onEasy?.Invoke());
            if (_mediumButton   != null) _mediumButton  .onClick.AddListener(() => _onMedium?.Invoke());
            if (_hardButton     != null) _hardButton    .onClick.AddListener(() => _onHard?.Invoke());

            HideCompletion();
        }

        public void SetNextLevelCallback(System.Action callback) => _onNextLevel = callback;
        public void SetDifficultyCallbacks(System.Action easy, System.Action medium, System.Action hard)
        {
            _onEasy   = easy;
            _onMedium = medium;
            _onHard   = hard;
        }

        public void ShowCompletion()
        {
            if (_completionPanel == null) return;
            _completionPanel.SetActive(true);

            if (_doneText != null)
            {
                var c = _doneText.color; c.a = 0f; _doneText.color = c;
                _doneText.DOFade(1f, 0.5f).SetEase(Ease.OutQuad);
            }
            if (_tapToContinueText != null)
            {
                var c = _tapToContinueText.color; c.a = 0f; _tapToContinueText.color = c;
                _tapToContinueText.DOFade(1f, 0.5f).SetDelay(0.4f).SetEase(Ease.OutQuad);
            }
        }

        public void HideCompletion()
        {
            if (_completionPanel == null) return;
            if (_doneText          != null) { _doneText.DOKill();          var c = _doneText.color;         c.a = 0f; _doneText.color         = c; }
            if (_tapToContinueText != null) { _tapToContinueText.DOKill(); var c = _tapToContinueText.color; c.a = 0f; _tapToContinueText.color = c; }
            _completionPanel.SetActive(false);
        }
    }
}
