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
        public bool HasScriptedInput => scriptedInput.HasValue;
        private Vector2? scriptedInput;
        private float slowUntil;
        private float slowedMultiplier = 1f;
        private Vector2 knockback;
        private float knockbackUntil;

        public void SetScriptedInput(Vector2? direction)
        {
            scriptedInput = direction;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        public void ApplySlow(float multiplier, float duration)
        {
            slowedMultiplier = Mathf.Clamp01(multiplier);
            slowUntil = Time.time + duration;
        }

        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            knockback = velocity;
            knockbackUntil = Time.time + duration;
        }

        public void ClearMovementEffects()
        {
            ClearSlow();
            knockbackUntil = 0f;
        }

        public void ClearSlow() => slowUntil = 0f;

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
            Vector2 direction = scriptedInput ?? input.MoveInput;
            float multiplier = Time.time < slowUntil ? slowedMultiplier : 1f;
            body.linearVelocity = direction * (moveSpeed * multiplier) +
                (Time.time < knockbackUntil ? knockback : Vector2.zero);
        }

        private void OnDisable()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
