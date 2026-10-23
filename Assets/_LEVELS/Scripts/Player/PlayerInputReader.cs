using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private static int lastConsumedInteractFrame = -1;
        [SerializeField] private string moveActionPath = "Player/Move";

        [SerializeField, Range(0f, 1f)] private float movementDeadzone = 0.1f;

        private InputAction moveAction;

        public Vector2 MoveInput
        {
            get
            {
                if (!Levels.Core.GameFlow.IsGameplayActive || moveAction == null || !moveAction.enabled) return Vector2.zero;
                Vector2 value = moveAction.ReadValue<Vector2>();
                return value.sqrMagnitude <= movementDeadzone * movementDeadzone
                    ? Vector2.zero
                    : Vector2.ClampMagnitude(value, 1f);
            }
        }

        public static bool TryConsumeInteractPress(InputAction action)
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || action == null || !action.WasPressedThisFrame() || lastConsumedInteractFrame == Time.frameCount)
                return false;

            lastConsumedInteractFrame = Time.frameCount;
            return true;
        }

        public static void ConsumeInteractForCurrentFrame()
        {
            lastConsumedInteractFrame = Time.frameCount;
        }

        private void OnEnable()
        {
            // Resolve through PlayerInput so we use its device pairing and action instance.
            moveAction = GetComponent<PlayerInput>().actions.FindAction(moveActionPath, true);
        }

        private void OnDisable()
        {
            moveAction = null;
        }
    }
}
