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

        private const float FadeDuration = 0.5f;
        private const float TapDelay     = 0.4f;

        private System.Action _onNextLevel;
        private System.Action _onEasy;
        private System.Action _onMedium;
        private System.Action _onHard;

        private void Awake()
        {
            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(() => _onNextLevel?.Invoke());
            if (_easyButton      != null) _easyButton    .onClick.AddListener(() => _onEasy?.Invoke());
            if (_mediumButton    != null) _mediumButton  .onClick.AddListener(() => _onMedium?.Invoke());
            if (_hardButton      != null) _hardButton    .onClick.AddListener(() => _onHard?.Invoke());

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
            FadeInText(_doneText,         FadeDuration);
            FadeInText(_tapToContinueText, FadeDuration, TapDelay);
        }

        public void HideCompletion()
        {
            if (_completionPanel == null) return;
            KillAndHideText(_doneText);
            KillAndHideText(_tapToContinueText);
            _completionPanel.SetActive(false);
        }

        private static void FadeInText(TMP_Text text, float duration, float delay = 0f)
        {
            if (text == null) return;
            var c = text.color; c.a = 0f; text.color = c;
            text.DOFade(1f, duration).SetDelay(delay).SetEase(Ease.OutQuad);
        }

        private static void KillAndHideText(TMP_Text text)
        {
            if (text == null) return;
            text.DOKill();
            var c = text.color; c.a = 0f; text.color = c;
        }
    }
}
