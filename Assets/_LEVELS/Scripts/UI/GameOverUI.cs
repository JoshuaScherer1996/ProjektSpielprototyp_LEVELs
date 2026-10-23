using Levels.Core;
using UnityEngine;

namespace Levels.UI
{
    public sealed class GameOverUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        public bool IsVisible => panel != null && panel.activeInHierarchy;
        private void Awake() => panel.SetActive(false);
        private void OnEnable() { retryButton.onClick.AddListener(Retry); mainMenuButton.onClick.AddListener(BackToMainMenu); }
        private void OnDisable() { retryButton.onClick.RemoveListener(Retry); mainMenuButton.onClick.RemoveListener(BackToMainMenu); }
        public void Show() => panel.SetActive(true);
        public void Retry() { if (GameFlow.Instance != null) GameFlow.Instance.StartNewRun(); }
        public void BackToMainMenu() { if (GameFlow.Instance != null) GameFlow.Instance.BackToMainMenu(); }
    }
}
