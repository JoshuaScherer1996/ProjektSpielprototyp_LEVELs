using Levels.Core;
using UnityEngine;

namespace Levels.UI
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button playButton;
        [SerializeField] private UnityEngine.UI.Button exitButton;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            exitButton.gameObject.SetActive(false);
            var navigation = playButton.navigation;
            navigation.selectOnUp = navigation.selectOnDown = playButton;
            playButton.navigation = navigation;
#endif
        }
        private void OnEnable() { playButton.onClick.AddListener(Play); exitButton.onClick.AddListener(Exit); }
        private void OnDisable() { playButton.onClick.RemoveListener(Play); exitButton.onClick.RemoveListener(Exit); }
        public void Play() { if (GameFlow.Instance != null) GameFlow.Instance.StartNewRun(); }
        public void Exit()
        {
            if (GameFlow.State != RunState.MainMenu) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }
    }
}
