using Levels.Core;
using TMPro;
using UnityEngine;

namespace Levels.UI
{
    public sealed class WinScreenUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelSummary;
        [SerializeField] private UnityEngine.UI.Button mainMenuButton;
        [SerializeField] private UnityEngine.UI.Button endGameButton;

        private void Awake()
        {
            if (GameSession.Instance.Victory is VictoryLevels levels)
                levelSummary.text = $"Dein Level: {levels.Player}\nGoblin Level: {levels.Goblin}\nCyclops Level: {levels.Cyclops}";
            else
                levelSummary.text = string.Empty;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            endGameButton.gameObject.SetActive(true);
#else
            endGameButton.gameObject.SetActive(false);
            var navigation = mainMenuButton.navigation;
            navigation.selectOnUp = navigation.selectOnDown = mainMenuButton;
            mainMenuButton.navigation = navigation;
#endif
        }

        private void OnEnable()
        {
            mainMenuButton.onClick.AddListener(BackToMainMenu);
            endGameButton.onClick.AddListener(EndGame);
        }

        private void OnDisable()
        {
            mainMenuButton.onClick.RemoveListener(BackToMainMenu);
            endGameButton.onClick.RemoveListener(EndGame);
        }

        private void BackToMainMenu() => GameFlow.Instance?.BackToMainMenu();

        private void EndGame()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (GameFlow.State == RunState.Victory) Application.Quit();
#endif
        }
    }
}
