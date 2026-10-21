using UnityEngine;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private PlayerInputReader input;

        public float MoveSpeed => moveSpeed;

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void FixedUpdate()
        {
            // Velocity participates in normal 2D collision resolution; never teleport the Transform.
            body.linearVelocity = input.MoveInput * moveSpeed;
        }

        private void OnDisable()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
