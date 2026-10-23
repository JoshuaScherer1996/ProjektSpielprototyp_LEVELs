using System.Collections;
using Levels.Core;
using Levels.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionService : MonoBehaviour
    {
        [SerializeField] private LoadingScreen loadingScreen;
        [SerializeField, Min(0f)] private float minimumLoadingSeconds = 2f;

        private static bool isTransitioning;
        private static string pendingScene;
        private static string pendingSpawnPointId;
        private static string pendingControlScheme;
        private static InputDevice[] pendingInputDevices;
        private static double minimumEndTime;
        private static int blockPortalUntilFrame = -1;
        private static bool runTransition;
        private static bool displayLoadingScreen = true;

        public LoadingScreen Screen => loadingScreen;
        public static bool IsTransitioning => isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
            pendingControlScheme = null;
            pendingInputDevices = null;
            minimumEndTime = 0;
            blockPortalUntilFrame = -1;
            runTransition = false;
            displayLoadingScreen = true;
        }

        public bool TryTransition(string destinationScene, string destinationSpawnPointId, PlayerMovement player)
        {
            if (!GameFlow.IsGameplayActive || isTransitioning || Time.frameCount <= blockPortalUntilFrame || player == null || !player.enabled)
                return false;

            if (string.IsNullOrWhiteSpace(destinationScene) ||
                string.IsNullOrWhiteSpace(destinationSpawnPointId) ||
                destinationScene == gameObject.scene.name ||
                !Application.CanStreamedLevelBeLoaded(destinationScene) ||
                player == null || player.gameObject.scene != gameObject.scene ||
                loadingScreen == null)
            {
                Debug.LogError($"Invalid transition from {gameObject.scene.name} to {destinationScene}/{destinationSpawnPointId}.", this);
                return false;
            }

            isTransitioning = true;
            pendingScene = destinationScene;
            pendingSpawnPointId = destinationSpawnPointId;
            PlayerInput sourceInput = player.GetComponent<PlayerInput>();
            pendingControlScheme = sourceInput.currentControlScheme;
            pendingInputDevices = new InputDevice[sourceInput.devices.Count];
            for (int i = 0; i < pendingInputDevices.Length; i++)
                pendingInputDevices[i] = sourceInput.devices[i];
            minimumEndTime = Time.realtimeSinceStartupAsDouble + minimumLoadingSeconds;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetPlayerControl(player, false);
            loadingScreen.Show();
            StartCoroutine(BeginLoad(player));
            return true;
        }

        // PLAY/RETRY/menu return share the existing black overlay, async loader and destination placement.
        // Unlike a portal, a run boundary can start after the source Player has been destroyed.
        public bool TryLoadRunScene(string destinationScene, string spawnPointId,
            bool showLoadingScreen = true, float? loadingSeconds = null)
        {
            if (isTransitioning || loadingScreen == null || string.IsNullOrWhiteSpace(destinationScene) ||
                !Application.CanStreamedLevelBeLoaded(destinationScene)) return false;
            isTransitioning = true;
            runTransition = true;
            displayLoadingScreen = showLoadingScreen;
            pendingScene = destinationScene;
            pendingSpawnPointId = spawnPointId;
            pendingControlScheme = null;
            pendingInputDevices = null;
            minimumEndTime = Time.realtimeSinceStartupAsDouble +
                (showLoadingScreen ? Mathf.Max(0f, loadingSeconds ?? minimumLoadingSeconds) : 0f);
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (displayLoadingScreen) loadingScreen.Show();
            StartCoroutine(BeginLoad(null));
            return true;
        }

        private IEnumerator BeginLoad(PlayerMovement sourcePlayer)
        {
            // Render the source scene's black overlay before beginning the async load.
            yield return null;
            if (!isTransitioning) yield break;
            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(pendingScene, LoadSceneMode.Single);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
            if (operation != null) yield break;

            bool failedRunTransition = runTransition;
            ResetPendingTransition();
            loadingScreen.Hide();
            SetPlayerControl(sourcePlayer, true);
            if (failedRunTransition) GameFlow.Instance?.RecoverLoadFailure();
            Debug.LogError("The destination scene could not be loaded.", this);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isTransitioning || scene.name != pendingScene)
                return;

            string spawnId = pendingSpawnPointId;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (runTransition && string.IsNullOrEmpty(spawnId))
            {
                var menuService = FindAnyObjectByType<SceneTransitionService>();
                if (menuService == null || menuService.loadingScreen == null)
                {
                    Debug.LogError("MainMenu needs its saved SceneTransitionService and LoadingScreen.");
                    ResetPendingTransition();
                    return;
                }
                if (displayLoadingScreen)
                {
                    menuService.loadingScreen.Show();
                    menuService.StartCoroutine(menuService.CompleteAfterMinimum(null));
                }
                else menuService.CompleteLoad(null);
                return;
            }

            SpawnPoint destination = null;
            PlayerMovement player = null;
            CameraFollow cameraFollow = null;
            SceneTransitionService targetService = null;
            int matchingSpawns = 0;
            int players = 0;
            int services = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpawnPoint point in root.GetComponentsInChildren<SpawnPoint>(true))
                {
                    if (point.Id != spawnId) continue;
                    destination = point;
                    matchingSpawns++;
                }

                foreach (PlayerMovement candidate in root.GetComponentsInChildren<PlayerMovement>(true))
                {
                    player = candidate;
                    players++;
                }

                foreach (CameraFollow follow in root.GetComponentsInChildren<CameraFollow>(true))
                    cameraFollow = follow;

                foreach (SceneTransitionService service in root.GetComponentsInChildren<SceneTransitionService>(true))
                {
                    targetService = service;
                    services++;
                }
            }

            if (matchingSpawns != 1 || players != 1 || player == null ||
                cameraFollow == null || cameraFollow.Target != player.transform ||
                services != 1 || targetService.loadingScreen == null)
            {
                Debug.LogError($"Scene {scene.name} needs exactly one spawn '{spawnId}', one Player and a camera following it. " +
                               $"Found {matchingSpawns} spawns, {players} players and {services} services.");
                ResetPendingTransition();
                return;
            }

            RestoreInputPairing(player.GetComponent<PlayerInput>());
            SetPlayerControl(player, false);
            if (displayLoadingScreen) targetService.loadingScreen.Show();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector3 position = destination.transform.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = new Vector2(position.x, position.y);
            Physics2D.SyncTransforms();
            cameraFollow.SnapToTarget();
            targetService.StartCoroutine(targetService.CompleteAfterMinimum(player));
        }

        private IEnumerator CompleteAfterMinimum(PlayerMovement player)
        {
            // The target overlay remains for at least one rendered frame even
            // when an unusually slow load has already exceeded the minimum.
            yield return null;
            while (Time.realtimeSinceStartupAsDouble < minimumEndTime)
                yield return null;

            CompleteLoad(player);
        }

        private void CompleteLoad(PlayerMovement player)
        {
            loadingScreen.Hide();
            bool completedRunTransition = runTransition;
            ResetPendingTransition();
            if (completedRunTransition) GameFlow.Instance?.CompleteSceneLoad();
            SetPlayerControl(player, true);
            blockPortalUntilFrame = Time.frameCount + 2;
        }

        private static void SetPlayerControl(PlayerMovement player, bool enabled)
        {
            if (player == null) return;
            enabled = enabled && GameFlow.IsGameplayActive;
            PlayerAttack attack = player.GetComponent<PlayerAttack>();
            if (enabled)
            {
                player.enabled = true;
                if (attack != null) attack.enabled = true;
            }
            else
            {
                if (attack != null) attack.enabled = false;
                player.enabled = false;
            }
        }

        private static void RestoreInputPairing(PlayerInput input)
        {
            if (string.IsNullOrEmpty(pendingControlScheme) || pendingInputDevices == null ||
                pendingInputDevices.Length == 0)
                return;

            foreach (InputDevice device in pendingInputDevices)
                if (device == null || !device.added)
                    return;

            input.SwitchCurrentControlScheme(pendingControlScheme, pendingInputDevices);
        }

        private static void ResetPendingTransition()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
            pendingControlScheme = null;
            pendingInputDevices = null;
            minimumEndTime = 0;
            runTransition = false;
            displayLoadingScreen = true;
        }
    }
}
