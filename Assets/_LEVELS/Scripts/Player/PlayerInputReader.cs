using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private string moveActionPath = "Player/Move";

        [SerializeField, Range(0f, 1f)] private float movementDeadzone = 0.1f;

        private InputAction moveAction;

        public Vector2 MoveInput
        {
            get
            {
                if (moveAction == null || !moveAction.enabled) return Vector2.zero;
                Vector2 value = moveAction.ReadValue<Vector2>();
                return value.sqrMagnitude <= movementDeadzone * movementDeadzone
                    ? Vector2.zero
                    : Vector2.ClampMagnitude(value, 1f);
            }
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
