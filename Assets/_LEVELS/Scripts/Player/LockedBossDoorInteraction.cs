using Levels.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    /// <summary>Message-only interaction. Never opens the solid door or starts a transition.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class LockedBossDoorInteraction : MonoBehaviour, IStationInteractionTarget
    {
        [SerializeField, Min(0f)] private float interactionRange = 1.25f;
        [SerializeField, Min(.1f)] private float messageSeconds = 4f;
        [SerializeField] private Collider2D blockingCollider;
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private GameObject messagePanel;
        private PlayerMovement player;
        private Collider2D playerCollider;
        private InputAction interact;
        private float hideAt;

        public bool IsMessageVisible => messagePanel != null && messagePanel.activeSelf;
        float IStationInteractionTarget.InteractionRange => interactionRange;
        int IStationInteractionTarget.SelectionPriority => 2;
        bool IStationInteractionTarget.IsAvailable => isActiveAndEnabled && player != null && player.enabled &&
            !SceneTransitionService.IsTransitioning;

        private void Reset() => blockingCollider = GetComponent<Collider2D>();
        private void OnEnable() => StationInteractionSelector.Register(this);
        private void OnDisable()
        {
            StationInteractionSelector.Unregister(this);
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
            HideMessage();
        }

        private void Start()
        {
            var target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.GetComponent<PlayerMovement>();
            playerCollider = target.GetComponent<Collider2D>();
            var input = target.GetComponent<PlayerInput>();
            if (input != null) interact = input.actions.FindAction("Player/Interact", true);
        }

        private void Update()
        {
            bool selected = ReferenceEquals(StationInteractionSelector.GetBestTarget(player), this);
            if (interactionPrompt != null) interactionPrompt.SetActive(selected && !IsMessageVisible);
            if (IsMessageVisible)
            {
                if (Time.unscaledTime >= hideAt || SceneTransitionService.IsTransitioning ||
                    (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                    (selected && PlayerInputReader.TryConsumeInteractPress(interact))) HideMessage();
            }
            else if (selected) ((IStationInteractionTarget)this).TryOpen();
        }

        float IStationInteractionTarget.DistanceFrom(Vector2 position) =>
            blockingCollider != null && playerCollider != null ? blockingCollider.Distance(playerCollider).distance :
            Vector2.Distance(position, transform.position);
        float IStationInteractionTarget.SelectionDistanceFrom(Vector2 position) => Vector2.Distance(position, transform.position);
        void IStationInteractionTarget.TryOpen()
        {
            if (PlayerInputReader.TryConsumeInteractPress(interact)) ShowMessage();
        }

        private void ShowMessage()
        {
            if (messagePanel == null) return;
            messagePanel.SetActive(true);
            hideAt = Time.unscaledTime + messageSeconds;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }

        private void HideMessage()
        {
            if (messagePanel != null) messagePanel.SetActive(false);
        }

        // Called synchronously from the mouse Attack callback, before a slash/cooldown is created.
        // Mouse clicks can inspect the door from anywhere on screen; keyboard/gamepad use range.
        public static bool TryHandlePointer(PlayerMovement source)
        {
            if (source == null || !source.enabled || SceneTransitionService.IsTransitioning ||
                Mouse.current == null || Camera.main == null) return false;
            Vector2 screen = Mouse.current.position.ReadValue();
            if (!Camera.main.pixelRect.Contains(screen)) return false;
            if (EventSystem.current != null)
            {
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                if (hits.Count > 0) return false;
            }
            Vector2 world = Camera.main.ScreenToWorldPoint(screen);
            foreach (var door in FindObjectsByType<LockedBossDoorInteraction>())
                if (door.gameObject.scene == source.gameObject.scene && door.blockingCollider != null &&
                    door.blockingCollider.OverlapPoint(world))
                {
                    door.ShowMessage();
                    return true;
                }
            return false;
        }
    }
}
