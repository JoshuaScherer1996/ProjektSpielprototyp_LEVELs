using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
    public sealed class EnemyAI : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float detectionRange = 4f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 4f;
        [SerializeField, Min(0f)] private float knockbackDuration = 0.16f;
        [SerializeField] private Rigidbody2D body;

        private Transform player;
        private bool touchingPlayer;
        private Vector2 knockbackDirection;
        private float knockbackEnd;

        public float DetectionRange => detectionRange;

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (Time.time < knockbackEnd)
            {
                float remaining = (knockbackEnd - Time.time) / Mathf.Max(0.001f, knockbackDuration);
                body.linearVelocity = knockbackDirection * (knockbackSpeed * remaining);
                return;
            }

            if (player == null)
            {
                GameObject target = GameObject.FindGameObjectWithTag("Player");
                if (target != null) player = target.transform;
            }

            Vector2 direction = player == null ? Vector2.zero :
                (Vector2)(player.position - transform.position);
            body.linearVelocity = !touchingPlayer && player != null &&
                direction.sqrMagnitude <= detectionRange * detectionRange
                ? direction.normalized * moveSpeed
                : Vector2.zero;
        }

        public void ApplyKnockback(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f || knockbackDuration <= 0f) return;
            knockbackDirection = direction.normalized;
            knockbackEnd = Time.time + knockbackDuration;
            body.linearVelocity = knockbackDirection * knockbackSpeed;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.CompareTag("Player")) touchingPlayer = true;
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.CompareTag("Player")) touchingPlayer = false;
        }

        private void OnDisable()
        {
            touchingPlayer = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
