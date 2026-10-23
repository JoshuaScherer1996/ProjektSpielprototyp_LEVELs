using Levels.Core;
using UnityEngine;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        [SerializeField] private PauseController controller;
        public bool IsVisible => panel != null && panel.activeInHierarchy;

        private void Awake() => panel.SetActive(false);
        private void OnEnable()
        {
            continueButton.onClick.AddListener(Continue);
            mainMenuButton.onClick.AddListener(BackToMainMenu);
        }
        private void OnDisable()
        {
            continueButton.onClick.RemoveListener(Continue);
            mainMenuButton.onClick.RemoveListener(BackToMainMenu);
        }
        public void Show() => panel.SetActive(true);
        public void Hide() => panel.SetActive(false);
        private void Continue() => controller.Continue();
        private void BackToMainMenu() => controller.BackToMainMenu();
    }
}
