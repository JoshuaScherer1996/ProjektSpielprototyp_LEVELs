using Levels.Audio;
using Levels.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Levels.UI
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject menuContent;
        [SerializeField] private GameObject developerCredit;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private ScrollRect creditsScroll;
        [SerializeField] private MenuFocus menuFocus;
        [SerializeField] private InputSystemUIInputModule inputModule;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            exitButton.gameObject.SetActive(false);
            var navigation = playButton.navigation;
            navigation.selectOnUp = navigation.selectOnDown = creditsButton;
            playButton.navigation = navigation;
            navigation = creditsButton.navigation;
            navigation.selectOnUp = navigation.selectOnDown = playButton;
            creditsButton.navigation = navigation;
#endif
        }

        private void OnEnable()
        {
            playButton.onClick.AddListener(Play);
            exitButton.onClick.AddListener(Exit);
            creditsButton.onClick.AddListener(OpenCredits);
            backButton.onClick.AddListener(CloseCredits);
            inputModule.cancel.action.performed += OnCancel;
        }

        private void OnDisable()
        {
            playButton.onClick.RemoveListener(Play);
            exitButton.onClick.RemoveListener(Exit);
            creditsButton.onClick.RemoveListener(OpenCredits);
            backButton.onClick.RemoveListener(CloseCredits);
            inputModule.cancel.action.performed -= OnCancel;
        }

        private void Update()
        {
            if (!creditsPanel.activeSelf) return;
            float direction = inputModule.move.action.ReadValue<Vector2>().y;
            float scrollRange = creditsScroll.content.rect.height - creditsScroll.viewport.rect.height;
            if (scrollRange <= 0f || Mathf.Abs(direction) < .25f) return;
            creditsScroll.StopMovement();
            creditsScroll.verticalNormalizedPosition = Mathf.Clamp01(creditsScroll.verticalNormalizedPosition
                + direction * 440f * Time.unscaledDeltaTime / scrollRange);
        }

        private void OnCancel(InputAction.CallbackContext context) => CloseCredits();

        public void Play()
        {
            if (GameFlow.Instance != null && GameFlow.Instance.StartNewRun()) GameAudio.Play(Cue.Confirm);
        }

        public void Exit()
        {
            if (GameFlow.State != RunState.MainMenu) return;
            GameAudio.ConfirmAndQuit();
        }

        private void OpenCredits()
        {
            if (GameFlow.State != RunState.MainMenu || creditsPanel.activeSelf) return;
            menuContent.SetActive(false);
            developerCredit.SetActive(false);
            creditsPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            creditsScroll.StopMovement();
            creditsScroll.verticalNormalizedPosition = 1f;
            menuFocus.Select(backButton);
            GameAudio.Play(Cue.Confirm);
        }

        private void CloseCredits()
        {
            if (!creditsPanel.activeSelf) return;
            creditsPanel.SetActive(false);
            menuContent.SetActive(true);
            developerCredit.SetActive(true);
            menuFocus.Select(creditsButton);
            GameAudio.Play(Cue.Confirm);
        }
    }
}
