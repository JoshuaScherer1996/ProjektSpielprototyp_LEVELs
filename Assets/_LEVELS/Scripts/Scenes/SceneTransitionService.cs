using System.Collections;
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
        [SerializeField, Min(0f)] private float minimumLoadingSeconds = 1.2f;

        private static bool isTransitioning;
        private static string pendingScene;
        private static string pendingSpawnPointId;
        private static double minimumEndTime;
        private static int blockPortalUntilFrame = -1;

        public LoadingScreen Screen => loadingScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
            minimumEndTime = 0;
            blockPortalUntilFrame = -1;
        }

        public bool TryTransition(string destinationScene, string destinationSpawnPointId, PlayerMovement player)
        {
            if (isTransitioning || Time.frameCount <= blockPortalUntilFrame)
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
            minimumEndTime = Time.realtimeSinceStartupAsDouble + minimumLoadingSeconds;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SetPlayerControl(player, false);
            loadingScreen.Show();
            StartCoroutine(BeginLoad(player));
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

            ResetPendingTransition();
            loadingScreen.Hide();
            SetPlayerControl(sourcePlayer, true);
            Debug.LogError("The destination scene could not be loaded.", this);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isTransitioning || scene.name != pendingScene)
                return;

            string spawnId = pendingSpawnPointId;
            SceneManager.sceneLoaded -= OnSceneLoaded;

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

            SetPlayerControl(player, false);
            targetService.loadingScreen.Show();
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

            loadingScreen.Hide();
            SetPlayerControl(player, true);
            ResetPendingTransition();
            blockPortalUntilFrame = Time.frameCount + 2;
        }

        private static void SetPlayerControl(PlayerMovement player, bool enabled)
        {
            if (enabled)
            {
                player.GetComponent<PlayerInput>().enabled = true;
                player.GetComponent<PlayerInputReader>().enabled = true;
                player.enabled = true;
            }
            else
            {
                player.enabled = false;
                player.GetComponent<PlayerInputReader>().enabled = false;
                player.GetComponent<PlayerInput>().enabled = false;
            }
        }

        private static void ResetPendingTransition()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
            minimumEndTime = 0;
        }
    }
}
