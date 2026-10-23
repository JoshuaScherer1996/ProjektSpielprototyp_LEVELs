using System;
using System.Collections.Generic;
using Levels.Player;
using Levels.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Levels.Enemies
{
    [Serializable]
    public sealed class EnemySpawnConfig
    {
        public string category = "Goblin";
        public EnemySpawnIdentity prefab;
        [Min(0)] public int maxAlive = 5;
        [Min(.01f)] public float spawnInterval = 2f;
    }

    /// <summary>One scene-local controller; categories have independent limits and scaled-time clocks.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemySpawnArea))]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemySpawnArea spawnArea;
        [SerializeField] private EnemySpawnConfig[] enemies = { new EnemySpawnConfig() };
        [SerializeField, Min(0f)] private float safeDistance = 3f;
        [SerializeField, Min(0f)] private float entranceClearance = 2f;
        [SerializeField, Min(0f)] private float colliderPadding = .1f;
        [SerializeField, Min(1)] private int attemptsPerInterval = 20;
        [Tooltip("Include Default (portals), World, Player and Enemy. Trigger colliders also block spawns.")]
        [SerializeField] private LayerMask blockingLayers = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 10);

        private float[] elapsed;
        private PlayerMovement player;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        public EnemySpawnArea SpawnArea => spawnArea;
        public IReadOnlyList<EnemySpawnConfig> Enemies => enemies;
        public float SafeDistance => safeDistance;
        public int LastAttemptCount { get; private set; }
        public event Action<EnemySpawnIdentity> Spawned;

        private void Reset() => spawnArea = GetComponent<EnemySpawnArea>();

        private void OnEnable()
        {
            if (spawnArea == null) spawnArea = GetComponent<EnemySpawnArea>();
            elapsed = new float[enemies.Length];
            // Multiple controllers for one scene would compete for the same category budget.
            foreach (EnemySpawner other in FindObjectsByType<EnemySpawner>())
                if (other != this && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene &&
                    other.elapsed != null)
                {
                    Debug.LogError("Only one EnemySpawner may run in a scene.", this);
                    enabled = false;
                    return;
                }
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnemySpawnConfig entry in enemies)
                if (entry == null || string.IsNullOrWhiteSpace(entry.category) || !categories.Add(entry.category) ||
                    entry.prefab == null || entry.prefab.Category != entry.category ||
                    !entry.prefab.gameObject.activeSelf || !TryGetFootprint(entry.prefab, out _))
                {
                    Debug.LogError("EnemySpawner requires unique categories and matching active prefabs with supported solid colliders.", this);
                    enabled = false;
                    return;
                }
        }

        private void OnDisable()
        {
            elapsed = null;
            LastAttemptCount = 0;
            player = null;
            overlaps.Clear();
        }

        private void Update()
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || Time.timeScale <= 0f || SceneTransitionService.IsTransitioning) return;
            // Inspector changes during Play mode also restart the clocks, with no catch-up burst.
            if (elapsed == null || elapsed.Length != enemies.Length) elapsed = new float[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemySpawnConfig entry = enemies[i];
                if (entry == null) continue;
                elapsed[i] += Time.deltaTime;
                if (elapsed[i] < Mathf.Max(.01f, entry.spawnInterval)) continue;
                elapsed[i] = 0f;
                TrySpawn(entry);
            }
        }

        public int CountAlive(string category)
        {
            int count = 0;
            // Reconcile actual scene objects at the interval, including disabled living enemies.
            // No death subscriptions, mutable counters, or retained enemy references can become stale.
            foreach (EnemySpawnIdentity enemy in FindObjectsByType<EnemySpawnIdentity>(
                         FindObjectsInactive.Include))
                if (enemy.gameObject.scene == gameObject.scene && enemy.Category == category && enemy.IsAlive) count++;
            return count;
        }

        private void TrySpawn(EnemySpawnConfig entry)
        {
            LastAttemptCount = 0;
            if (!Levels.Core.GameFlow.IsGameplayActive || !isActiveAndEnabled) return;
            if (entry.prefab == null || entry.prefab.Category != entry.category ||
                CountAlive(entry.category) >= Mathf.Max(0, entry.maxAlive) || spawnArea == null ||
                spawnArea.gameObject.scene != gameObject.scene || !TryGetFootprint(entry.prefab, out Bounds shape)) return;
            if (player == null || player.gameObject.scene != gameObject.scene)
                foreach (PlayerMovement candidate in FindObjectsByType<PlayerMovement>())
                    if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }
            if (player == null) return; // Never spawn without a known current player position.

            ScenePortal[] portals = FindObjectsByType<ScenePortal>(FindObjectsInactive.Include);
            SpawnPoint[] entrances = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            Physics2D.SyncTransforms();
            for (int attempt = 0; attempt < Mathf.Max(1, attemptsPerInterval); attempt++)
            {
                LastAttemptCount++;
                Vector2 position = spawnArea.RandomPosition();
                Bounds footprint = shape;
                footprint.center += (Vector3)position;
                if (!IsPositionValid(footprint, player.transform.position, portals, entrances)) continue;
                // Assign scene ownership explicitly, including when several scenes are loaded additively.
                EnemySpawnIdentity enemy = Instantiate(entry.prefab, position, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(enemy.gameObject, gameObject.scene);
                Physics2D.SyncTransforms();
                Spawned?.Invoke(enemy);
                return;
            }
        }

        private bool IsPositionValid(Bounds footprint, Vector2 playerPosition, ScenePortal[] portals, SpawnPoint[] entrances)
        {
            if (!spawnArea.Contains(footprint) ||
                Vector2.Distance(footprint.ClosestPoint(playerPosition), playerPosition) < Mathf.Max(0f, safeDistance)) return false;
            float clearance = Mathf.Max(0f, entranceClearance);
            foreach (SpawnPoint entrance in entrances)
                if (entrance.gameObject.scene == gameObject.scene &&
                    Vector2.Distance(footprint.ClosestPoint(entrance.transform.position), entrance.transform.position) <= clearance) return false;
            foreach (ScenePortal portal in portals)
            {
                if (portal.gameObject.scene != gameObject.scene) continue;
                Bounds exclusion = portal.GetComponent<Collider2D>().bounds;
                exclusion.Expand(new Vector3(clearance * 2f, clearance * 2f, 0f));
                if (OverlapsXY(footprint, exclusion)) return false;
            }
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(blockingLayers);
            overlaps.Clear();
            // Unity 6.6 also applies the global trigger gate to filtered overlap queries.
            // Scope the override to this synchronous query and restore it before any callbacks.
            bool previousTriggers = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = true;
                Physics2D.OverlapBox(footprint.center, footprint.size, 0f, filter, overlaps);
            }
            finally { Physics2D.queriesHitTriggers = previousTriggers; }
            return overlaps.Count == 0;
        }

        private static bool OverlapsXY(Bounds a, Bounds b) =>
            a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;

        private bool TryGetFootprint(EnemySpawnIdentity prefab, out Bounds footprint)
        {
            footprint = default;
            bool found = false;
            foreach (Collider2D collider in prefab.GetComponentsInChildren<Collider2D>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector2 size;
                Vector2 center = collider.offset;
                if (collider is BoxCollider2D box) size = box.size + Vector2.one * (box.edgeRadius * 2f);
                else if (collider is CircleCollider2D circle) size = Vector2.one * (circle.radius * 2f);
                else if (collider is CapsuleCollider2D capsule) size = capsule.size;
                else if (collider is PolygonCollider2D polygon)
                {
                    Bounds polygonBounds = default;
                    bool first = true;
                    for (int path = 0; path < polygon.pathCount; path++)
                    foreach (Vector2 point in polygon.GetPath(path))
                    {
                        if (first) { polygonBounds = new Bounds(point, Vector3.zero); first = false; }
                        else polygonBounds.Encapsulate(point);
                    }
                    if (first) return false;
                    size = polygonBounds.size;
                    center += (Vector2)polygonBounds.center;
                }
                else return false; // Unsupported geometry fails closed instead of guessing a radius.
                Matrix4x4 matrix = Matrix4x4.Scale(prefab.transform.localScale) *
                                   prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(center + Vector2.Scale(size * .5f, new Vector2(x, y)));
                    if (!found) { footprint = new Bounds(point, Vector3.zero); found = true; }
                    else footprint.Encapsulate(point);
                }
            }
            footprint.Expand(new Vector3(Mathf.Max(0f, colliderPadding) * 2f, Mathf.Max(0f, colliderPadding) * 2f, 0f));
            return found && footprint.size.x > 0f && footprint.size.y > 0f;
        }

        private void OnDrawGizmosSelected()
        {
            if (player == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.transform.position, safeDistance);
        }
    }
}
