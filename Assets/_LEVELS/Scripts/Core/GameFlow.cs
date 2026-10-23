using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using Levels.UI;
using UnityEngine;

namespace Levels.Core
{
    public enum RunState { MainMenu, Playing, GameOver, Loading }

    /// <summary>Scene-owned flow; only GameSession persists. All UI/world objects are saved assets.</summary>
    [DefaultExecutionOrder(-900), DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] private SceneTransitionService transitions;
        [SerializeField] private GameOverUI gameOverUI;
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string firstScene = "Village";
        [SerializeField] private string firstSpawnPoint = "NewRun";
        [SerializeField, Min(0f)] private float playLoadingSeconds = 1f;

        public static GameFlow Instance { get; private set; }
        public static RunState State { get; private set; } = RunState.Playing;
        public static bool IsGameplayActive => State == RunState.Playing && !SceneTransitionService.IsTransitioning;
        private RunState beforeLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; State = RunState.Playing; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GameFlow is allowed in the loaded scene.", this);
                enabled = false;
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
            if (gameObject.scene.name == mainMenuScene)
            {
                if (State != RunState.Loading) State = RunState.MainMenu;
                GameSession.Instance.EndRun();
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool StartNewRun()
        {
            if (State != RunState.MainMenu && State != RunState.GameOver) return false;
            return LoadRunScene(firstScene, firstSpawnPoint, true);
        }

        public bool BackToMainMenu()
        {
            if (State != RunState.GameOver) return false;
            return LoadRunScene(mainMenuScene, null, false);
        }

        private bool LoadRunScene(string scene, string spawn, bool newRun)
        {
            float? loadingSeconds = newRun && State == RunState.MainMenu ? playLoadingSeconds : null;
            if (transitions == null || !transitions.TryLoadRunScene(scene, spawn, newRun, loadingSeconds)) return false;
            beforeLoad = State;
            State = RunState.Loading; // Set the gate before closing any UI can restore movement.
            Time.timeScale = 1f;
            StopGameplay();
            if (newRun) GameSession.Instance.ResetForNewRun();
            else GameSession.Instance.EndRun();
            return true;
        }

        public void PlayerDied()
        {
            if (State != RunState.Playing) return;
            State = RunState.GameOver; // Synchronous gate precedes all death/health observers.
            Time.timeScale = 1f;
            StopGameplay();
            if (gameOverUI != null) gameOverUI.Show();
            else Debug.LogError("GameFlow needs a saved GameOverUI reference.", this);
        }

        private static void StopGameplay()
        {
            foreach (var menu in FindObjectsByType<ShopMenu>()) menu.Close();
            foreach (var menu in FindObjectsByType<LevelingMenu>()) menu.Close();
            foreach (var player in FindObjectsByType<PlayerMovement>())
            {
                player.enabled = false;
                var attack = player.GetComponent<PlayerAttack>();
                if (attack != null) attack.enabled = false;
            }
            foreach (var spawner in FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
            foreach (var ai in FindObjectsByType<EnemyAI>()) ai.enabled = false;
            foreach (var damage in FindObjectsByType<ContactDamage>()) damage.enabled = false;
            PlayerInputReader.ConsumeInteractForCurrentFrame();
        }

        public void CompleteSceneLoad()
        {
            State = gameObject.scene.name == mainMenuScene ? RunState.MainMenu : RunState.Playing;
            Time.timeScale = 1f;
            PlayerInputReader.ConsumeInteractForCurrentFrame();
        }

        public void RecoverLoadFailure()
        {
            State = beforeLoad;
            if (State == RunState.GameOver && gameOverUI != null) gameOverUI.Show();
        }
    }
}
