using Levels.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionService : MonoBehaviour
    {
        private static bool isTransitioning;
        private static string pendingScene;
        private static string pendingSpawnPointId;
        private static int blockPortalUntilFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
            blockPortalUntilFrame = -1;
        }

        public bool TryTransition(string destinationScene, string destinationSpawnPointId)
        {
            if (isTransitioning || Time.frameCount <= blockPortalUntilFrame)
                return false;

            if (string.IsNullOrWhiteSpace(destinationScene) ||
                string.IsNullOrWhiteSpace(destinationSpawnPointId) ||
                destinationScene == gameObject.scene.name ||
                !Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                Debug.LogError($"Invalid transition from {gameObject.scene.name} to {destinationScene}/{destinationSpawnPointId}.", this);
                return false;
            }

            isTransitioning = true;
            pendingScene = destinationScene;
            pendingSpawnPointId = destinationSpawnPointId;
            SceneManager.sceneLoaded += OnSceneLoaded;

            try
            {
                if (SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single) != null)
                    return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }

            ResetPendingTransition();
            return false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isTransitioning || scene.name != pendingScene)
                return;

            string spawnId = pendingSpawnPointId;
            ResetPendingTransition();
            // A newly loaded scene must not immediately send its player back if a
            // spawn point was accidentally placed inside a portal trigger.
            blockPortalUntilFrame = Time.frameCount + 2;

            SpawnPoint destination = null;
            PlayerMovement player = null;
            CameraFollow cameraFollow = null;
            int matchingSpawns = 0;
            int players = 0;

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
            }

            if (matchingSpawns != 1 || players != 1 || player == null ||
                cameraFollow == null || cameraFollow.Target != player.transform)
            {
                Debug.LogError($"Scene {scene.name} needs exactly one spawn '{spawnId}', one Player and a camera following it. " +
                               $"Found {matchingSpawns} spawns and {players} players.");
                return;
            }

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector3 position = destination.transform.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = new Vector2(position.x, position.y);
            Physics2D.SyncTransforms();
            cameraFollow.SnapToTarget();
        }

        private static void ResetPendingTransition()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            isTransitioning = false;
            pendingScene = null;
            pendingSpawnPointId = null;
        }
    }
}
