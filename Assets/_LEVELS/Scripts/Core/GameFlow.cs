using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using Levels.UI;
using UnityEngine;

namespace Levels.Core
{
    public enum RunState { MainMenu, Playing, GameOver, Loading, Paused }

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
        private readonly System.Collections.Generic.List<Behaviour> pausedControls = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; State = RunState.Playing; Time.timeScale = 1f; }

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

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (State == RunState.Paused) { State = RunState.Playing; Time.timeScale = 1f; }
            Instance = null;
        }

        public bool TryPause()
        {
            if (!IsGameplayActive) return false;
            foreach (var menu in FindObjectsByType<ShopMenu>()) if (menu.IsOpen) return false;
            foreach (var menu in FindObjectsByType<LevelingMenu>()) if (menu.IsOpen) return false;
            State = RunState.Paused; // Damage/input gates close synchronously, before any observers.
            pausedControls.Clear();
            foreach (var movement in FindObjectsByType<PlayerMovement>())
            {
                PauseControl(movement);
                PauseControl(movement.GetComponent<PlayerAttack>());
            }
            // Keep AI enabled: aggro, contact and knockback state survive the frozen physics clock.
            foreach (var ai in FindObjectsByType<EnemyAI>())
                ai.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            PlayerInputReader.ConsumeInteractForCurrentFrame();
            Time.timeScale = 0f;
            return true;
        }

        private void PauseControl(Behaviour control)
        {
            if (control == null || !control.enabled) return;
            pausedControls.Add(control);
            control.enabled = false;
        }

        public bool Resume()
        {
            if (State != RunState.Paused || SceneTransitionService.IsTransitioning) return false;
            State = RunState.Playing;
            Time.timeScale = 1f;
            // PlayerInput remains enabled throughout. MoveInput reads the still-held stick directly.
            foreach (var control in pausedControls) if (control != null) control.enabled = true;
            pausedControls.Clear();
            PlayerInputReader.ConsumeInteractForCurrentFrame();
            return true;
        }

        public bool StartNewRun()
        {
            if (State != RunState.MainMenu && State != RunState.GameOver) return false;
            return LoadRunScene(firstScene, firstSpawnPoint, true);
        }

        public bool BackToMainMenu()
        {
            if (State != RunState.GameOver && State != RunState.Paused) return false;
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
            pausedControls.Clear();
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
                var input = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (input != null) input.DeactivateInput();
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
