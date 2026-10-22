using Levels.Player;
using UnityEngine;

namespace Levels.Scenes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ScenePortal : MonoBehaviour
    {
        [SerializeField] private SceneTransitionService transitionService;
        [SerializeField] private string destinationScene;
        [SerializeField] private string destinationSpawnPointId;

        public string DestinationScene => destinationScene;
        public string DestinationSpawnPointId => destinationSpawnPointId;
        public SceneTransitionService TransitionService => transitionService;

        private void Reset()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player") ||
                !other.TryGetComponent(out PlayerMovement player))
                return;

            if (transitionService == null)
            {
                Debug.LogError($"Portal {name} has no SceneTransitionService.", this);
                return;
            }

            transitionService.TryTransition(destinationScene, destinationSpawnPointId, player);
        }
    }
}
