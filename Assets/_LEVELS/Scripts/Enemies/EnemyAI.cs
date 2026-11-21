using System.Collections.Generic;
using Levels.Audio;
using Levels.Scenes;
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
        [SerializeField, Min(0f)] private float wanderRadius;
        [SerializeField] private Vector2 wanderWait = new Vector2(1f, 3f);
        [SerializeField] private LayerMask wanderBlockingLayers = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10);

        private Transform player;
        private PlayerHealth playerHealth;
        private PlayerMovement playerMovement;
        private EnemyHealth health;
        private bool hasAggro;
        private bool touchingPlayer;
        private Vector2 knockbackDirection;
        private float knockbackEnd;
        private EnemyProgression progression;
        private Vector2 stepPosition;
        private Vector2 previousStepDirection;
        private float stepDistance;
        private EnemySpawnArea wanderArea;
        private Collider2D wanderCollider;
        private Vector2 wanderAnchor;
        private Vector2 wanderTarget;
        private Bounds reservedFootprint;
        private float wanderReadyAt;
        private bool isWandering;
        private readonly List<RaycastHit2D> wanderHits = new();
        private static readonly HashSet<EnemyAI> WanderReservations = new();

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
            progression = GetComponent<EnemyProgression>();
            if (wanderRadius > 0f) wanderCollider = GetComponent<Collider2D>();
            wanderAnchor = body.position;
            if (wanderRadius > 0f) WaitToWander();
        }

        private void Start()
        {
            if (wanderRadius <= 0f || wanderCollider == null || wanderArea != null) return;
            foreach (var area in FindObjectsByType<EnemySpawnArea>())
                if (area.gameObject.scene == gameObject.scene && area.Contains(wanderCollider.bounds))
                {
                    SetWanderArea(area);
                    break;
                }
        }

        internal void SetWanderArea(EnemySpawnArea area) => wanderArea = area;

        private void FixedUpdate()
        {
            Vector2 traveled = body.position - stepPosition;
            Vector2 lastDirection = previousStepDirection;
            stepPosition = body.position;
            previousStepDirection = Vector2.zero;
            if (!Levels.Core.GameFlow.IsGameplayActive)
            {
                hasAggro = false;
                if (isWandering) WaitToWander();
                body.linearVelocity = Vector2.zero;
                return;
            }
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
                if (isWandering) WaitToWander();
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
            bool wasAggro = hasAggro;
            hasAggro = detected || (persistentAggro && hasAggro);
            if (hasAggro || touchingPlayer)
            {
                if (isWandering) WaitToWander();
                body.linearVelocity = !touchingPlayer ? direction.normalized * MoveSpeed : Vector2.zero;
            }
            else
            {
                if (wasAggro && wanderRadius > 0f) WaitToWander();
                body.linearVelocity = WanderVelocity();
            }
            if (progression != null && body.linearVelocity.sqrMagnitude > .01f)
            {
                previousStepDirection = body.linearVelocity.normalized;
                GameAudio.EnemyStride(ref stepDistance, Vector2.Dot(traveled, lastDirection), progression.Type, body.position);
            }
            else stepDistance = 0f;
        }

        private Vector2 WanderVelocity()
        {
            if (wanderRadius <= 0f || wanderArea == null || wanderCollider == null ||
                !wanderCollider.enabled) return EndWander();

            if (!isWandering && (Time.time < wanderReadyAt || !TryChooseWanderTarget())) return Vector2.zero;

            Vector2 remaining = wanderTarget - body.position;
            float distance = remaining.magnitude;
            float speed = MoveSpeed * .5f;
            float step = speed * Time.fixedDeltaTime;
            if (speed <= 0f || distance <= Mathf.Max(.015f, step)) return EndWander();

            Vector2 direction = remaining / distance;
            if (!WanderPathIsClear(direction * (step + .03f))) return EndWander();

            return direction * speed;
        }

        private bool TryChooseWanderTarget()
        {
            if (!wanderArea.Contains(wanderCollider.bounds))
            {
                WaitToWander();
                return false;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 target = wanderAnchor + Random.insideUnitCircle * wanderRadius;
                Vector2 displacement = target - body.position;
                if (displacement.sqrMagnitude < .09f || !WanderPathIsClear(displacement)) continue;

                Bounds footprint = wanderCollider.bounds;
                footprint.center += (Vector3)displacement;
                footprint.Expand(new Vector3(.08f, .08f, 0f));
                if (!wanderArea.Contains(footprint)) continue;

                bool reserved = false;
                foreach (EnemyAI other in WanderReservations)
                {
                    if (other != this && other.gameObject.scene == gameObject.scene &&
                        other.reservedFootprint.Intersects(footprint))
                    {
                        reserved = true;
                        break;
                    }
                }
                if (reserved) continue;

                wanderTarget = target;
                reservedFootprint = footprint;
                isWandering = true;
                WanderReservations.Add(this);
                return true;
            }
            WaitToWander();
            return false;
        }

        private bool WanderPathIsClear(Vector2 displacement)
        {
            Bounds start = wanderCollider.bounds;
            Bounds end = start;
            end.center += (Vector3)displacement;
            if (!wanderArea.Contains(start) || !wanderArea.Contains(end)) return false;

            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(wanderBlockingLayers);
            bool previousTriggers = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = true;
                Physics2D.BoxCast(start.center, start.size, 0f, displacement.normalized,
                    filter, wanderHits, displacement.magnitude);
            }
            finally { Physics2D.queriesHitTriggers = previousTriggers; }

            foreach (RaycastHit2D hit in wanderHits)
            {
                Collider2D obstacle = hit.collider;
                if (obstacle == null || obstacle.attachedRigidbody == body ||
                    obstacle.gameObject.scene != gameObject.scene) continue;
                if (!obstacle.isTrigger || obstacle.GetComponentInParent<ScenePortal>() != null) return false;
            }
            return true;
        }

        private Vector2 EndWander()
        {
            if (isWandering) WaitToWander();
            return Vector2.zero;
        }

        private void WaitToWander()
        {
            WanderReservations.Remove(this);
            isWandering = false;
            wanderTarget = default;
            float minimum = Mathf.Max(.01f, wanderWait.x);
            wanderReadyAt = Time.time + Random.Range(minimum, Mathf.Max(minimum, wanderWait.y));
        }

        public void ApplyKnockback(Vector2 direction)
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || direction.sqrMagnitude < 0.001f || knockbackDuration <= 0f) return;
            if (isWandering) WaitToWander();
            previousStepDirection = Vector2.zero;
            stepDistance = 0f;
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
            if (isWandering) WaitToWander();
            previousStepDirection = Vector2.zero;
            stepDistance = 0f;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
