using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private string moveActionPath = "Player/Move";

        private InputAction moveAction;

        public Vector2 MoveInput { get; private set; }

        private void OnEnable()
        {
            // Resolve through PlayerInput so we use its device pairing and action instance.
            moveAction = GetComponent<PlayerInput>().actions.FindAction(moveActionPath, true);
            moveAction.performed += ReadMovement;
            moveAction.canceled += ReadMovement;
            MoveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.performed -= ReadMovement;
                moveAction.canceled -= ReadMovement;
                moveAction = null;
            }
            MoveInput = Vector2.zero;
        }

        private void ReadMovement(InputAction.CallbackContext context)
        {
            MoveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
        }
    }
}
