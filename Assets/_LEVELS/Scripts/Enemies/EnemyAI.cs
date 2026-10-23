using UnityEngine;
using Levels.Player;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
    public sealed class EnemyAI : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float detectionRange = 4f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField] private bool persistentAggro;
        [SerializeField] private bool usePlayerSpeed;
        [SerializeField, Range(0f, 1f)] private float playerSpeedRatio = .65f;
        [SerializeField, Min(0f)] private float knockbackSpeed = 4f;
        [SerializeField, Min(0f)] private float knockbackDuration = 0.16f;
        [SerializeField] private Rigidbody2D body;

        private Transform player;
        private PlayerHealth playerHealth;
        private PlayerMovement playerMovement;
        private EnemyHealth health;
        private bool hasAggro;
        private bool touchingPlayer;
        private Vector2 knockbackDirection;
        private float knockbackEnd;

        public float DetectionRange => detectionRange;
        public bool HasAggro => hasAggro;
        public float MoveSpeed => usePlayerSpeed && playerMovement != null
            ? playerMovement.MoveSpeed * Mathf.Clamp01(playerSpeedRatio) : moveSpeed;

        private void Reset()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            health = GetComponent<EnemyHealth>();
        }

        private void FixedUpdate()
        {
            if (player == null)
            {
                GameObject target = GameObject.FindGameObjectWithTag("Player");
                if (target != null && target.scene == gameObject.scene)
                {
                    player = target.transform;
                    playerHealth = target.GetComponent<PlayerHealth>();
                    playerMovement = target.GetComponent<PlayerMovement>();
                }
            }
            if (health == null || !health.IsAlive || player == null || playerHealth == null || playerHealth.CurrentHealth <= 0)
            {
                hasAggro = false;
                body.linearVelocity = Vector2.zero;
                return;
            }
            if (Time.time < knockbackEnd)
            {
                float remaining = (knockbackEnd - Time.time) / Mathf.Max(0.001f, knockbackDuration);
                body.linearVelocity = knockbackDirection * (knockbackSpeed * remaining);
                return;
            }

            Vector2 direction = player == null ? Vector2.zero :
                (Vector2)(player.position - transform.position);
            bool detected = direction.sqrMagnitude <= detectionRange * detectionRange;
            hasAggro = detected || (persistentAggro && hasAggro);
            body.linearVelocity = !touchingPlayer && hasAggro
                ? direction.normalized * MoveSpeed
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
            hasAggro = false;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
