using Levels.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    internal static class StationInteractionSelector
    {
        private static readonly System.Collections.Generic.List<IStationInteractionTarget> Targets = new();

        public static void Register(IStationInteractionTarget target)
        {
            if (target != null && !Targets.Contains(target)) Targets.Add(target);
        }

        public static void Unregister(IStationInteractionTarget target)
        {
            if (target != null) Targets.Remove(target);
        }

        public static IStationInteractionTarget GetBestTarget(PlayerMovement player)
        {
            if (player == null || !player.enabled) return null;

            IStationInteractionTarget best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (IStationInteractionTarget target in Targets)
            {
                if (target == null || !target.IsAvailable) continue;
                float distance = target.DistanceFrom(player.transform.position);
                if (distance > target.InteractionRange) continue;
                float selectionDistance = target.SelectionDistanceFrom(player.transform.position);

                if (selectionDistance < bestDistance ||
                    (Mathf.Approximately(selectionDistance, bestDistance) &&
                     (best == null || target.SelectionPriority > best.SelectionPriority)))
                {
                    best = target;
                    bestDistance = selectionDistance;
                }
            }

            return best;
        }
    }

    internal interface IStationInteractionTarget
    {
        float InteractionRange { get; }
        int SelectionPriority { get; }
        bool IsAvailable { get; }
        float DistanceFrom(Vector2 position);
        float SelectionDistanceFrom(Vector2 position);
        void TryOpen();
    }

    [DisallowMultipleComponent]
    public sealed class LevelingStation : MonoBehaviour, IStationInteractionTarget
    {
        [SerializeField, Min(0f)] private float interactionRange = 0.1f;
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private LevelingMenu menu;
        [SerializeField] private GameObject interactionPrompt;
        private PlayerMovement player;
        private Collider2D playerCollider;
        private InputAction interact;

        float IStationInteractionTarget.InteractionRange => interactionRange;
        int IStationInteractionTarget.SelectionPriority => 0;
        bool IStationInteractionTarget.IsAvailable => isActiveAndEnabled && player != null && menu != null && !menu.IsOpen;

        private void Reset()
        {
            blockingCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            StationInteractionSelector.Register(this);
        }

        private void OnDisable()
        {
            StationInteractionSelector.Unregister(this);
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }

        private void Start()
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.GetComponent<PlayerMovement>();
            playerCollider = target.GetComponent<Collider2D>();
            PlayerInput playerInput = target.GetComponent<PlayerInput>();
            if (playerInput != null)
                interact = playerInput.actions.FindAction("Player/Interact", true);
        }

        private void Update()
        {
            IStationInteractionTarget selected = StationInteractionSelector.GetBestTarget(player);
            bool isSelected = ReferenceEquals(selected, this);
            if (interactionPrompt != null) interactionPrompt.SetActive(isSelected);
            if (isSelected) ((IStationInteractionTarget)this).TryOpen();
        }

        float IStationInteractionTarget.DistanceFrom(Vector2 position)
        {
            if (blockingCollider != null && playerCollider != null)
                return blockingCollider.Distance(playerCollider).distance;
            if (blockingCollider == null) return Vector2.Distance(position, transform.position);
            Vector2 surfacePoint = blockingCollider.ClosestPoint(position);
            return Vector2.Distance(position, surfacePoint);
        }

        float IStationInteractionTarget.SelectionDistanceFrom(Vector2 position) =>
            Vector2.Distance(position, transform.position);

        void IStationInteractionTarget.TryOpen()
        {
            if (menu != null && player != null && !menu.IsOpen &&
                PlayerInputReader.TryConsumeInteractPress(interact))
                menu.Open(player);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (blockingCollider != null)
            {
                Bounds bounds = blockingCollider.bounds;
                float blockerRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                Gizmos.DrawWireSphere(blockingCollider.bounds.center, blockerRadius + interactionRange);
            }
            else Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
