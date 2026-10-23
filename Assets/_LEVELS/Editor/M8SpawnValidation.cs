using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Levels.Core;
using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Repeatable integration tests against the saved scenes in the real Editor player loop.
    /// Temporary objects, stat purchases and inspector overrides exist only in Play mode.</summary>
    [InitializeOnLoad]
    public static class M8SpawnValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public string startedUtc;
            public List<string> passed = new List<string>();
            public List<string> errors = new List<string>();
            public int observedSpawns;
            public float minimumPlayerClearance = float.MaxValue;
            public int firstFrame;
            public int lastFrame;
            public List<Vector2> firstSpawnPositions = new List<Vector2>();
        }

        private const string Pending = "LEVELS.M8.ValidationPending";
        private const string ResultPath = "Library/M8SpawnValidation.json";
        private static Report report;
        private static EnemySpawner spawner;
        private static readonly List<float> spawnTimes = new List<float>();
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static M8SpawnValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("LEVEL's/M8/Run Spawn Play Mode Validation")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before validation.");
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(M8SpawnSetup.PlateauPath);
            LevelsPlayModeSetup.UseValidationScene(M8SpawnSetup.PlateauPath);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            report = new Report { startedUtc = DateTime.UtcNow.ToString("o"), firstFrame = Time.frameCount };
            spawnTimes.Clear();
            Application.logMessageReceived += CaptureError;
            GameSession.Instance.StartCoroutine(GuardedRun());
        }

        private static IEnumerator GuardedRun()
        {
            IEnumerator tests = Run();
            while (true)
            {
                object next;
                try
                {
                    if (!tests.MoveNext()) break;
                    next = tests.Current;
                }
                catch (Exception exception) { report.errors.Add(exception.ToString()); break; }
                File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
                yield return next;
            }
            Time.timeScale = 1f;
            report.lastFrame = Time.frameCount;
            report.status = report.errors.Count == 0 ? "passed" : "failed";
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            Debug.Log($"M8 spawn validation: {report.status}, {report.passed.Count} checks, {report.observedSpawns} observed spawns. {ResultPath}");
            EditorApplication.isPlaying = false;
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + stack);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.passed.Add(message);
        }

        private static EnemySpawnIdentity[] Living() => Object.FindObjectsByType<EnemySpawnIdentity>(
            FindObjectsInactive.Include).Where(e => e.IsAlive && e.Category == "Goblin" &&
            e.gameObject.scene == spawner.gameObject.scene).ToArray();

        private static void RestartClock() { spawner.enabled = false; spawner.enabled = true; }

        private static void FreezeEnemies()
        {
            foreach (var enemy in Living())
            {
                enemy.GetComponent<EnemyAI>().enabled = false;
                enemy.GetComponent<ContactDamage>().enabled = false;
            }
        }

        private static void Observe(EnemySpawnIdentity enemy)
        {
            enemy.GetComponent<EnemyAI>().enabled = false;
            enemy.GetComponent<ContactDamage>().enabled = false;
            report.observedSpawns++;
            spawnTimes.Add(Time.time);
            var collider = enemy.GetComponent<Collider2D>();
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            Bounds bounds = collider.bounds;
            bounds.Expand(new Vector3(.2f, .2f, 0f));
            float clearance = Vector2.Distance(bounds.ClosestPoint(player.transform.position), player.transform.position);
            report.minimumPlayerClearance = Mathf.Min(report.minimumPlayerClearance, clearance);
            if (clearance < spawner.SafeDistance || !spawner.SpawnArea.Contains(bounds) || spawner.CountAlive("Goblin") > 5)
                report.errors.Add("Spawn exceeded count, area or player-safe-distance constraint.");
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(LayerMask.GetMask("Default", "World", "Player", "Enemy"));
            var hits = new List<Collider2D>();
            Physics2D.OverlapBox(bounds.center, bounds.size, 0f, filter, hits);
            if (hits.Any(hit => !hit.transform.IsChildOf(enemy.transform))) report.errors.Add("Spawn overlaps another collider.");
        }

        private static bool Valid(Vector2 position)
        {
            var args = new object[] { spawner.Enemies[0].prefab, default(Bounds) };
            bool supported = (bool)typeof(EnemySpawner).GetMethod("TryGetFootprint", PrivateInstance).Invoke(spawner, args);
            if (!supported) throw new InvalidOperationException("Prefab footprint unavailable.");
            Bounds footprint = (Bounds)args[1]; footprint.center += (Vector3)position;
            Physics2D.SyncTransforms();
            return (bool)typeof(EnemySpawner).GetMethod("IsPositionValid", PrivateInstance).Invoke(spawner, new object[] {
                footprint, (Vector2)Object.FindAnyObjectByType<PlayerMovement>().transform.position,
                Object.FindObjectsByType<ScenePortal>(FindObjectsInactive.Include),
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include) });
        }

        private static void SetFloat(string name, float value)
        {
            var so = new SerializedObject(spawner); so.FindProperty(name).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator Run()
        {
            yield return null;
            spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Check(spawner != null && spawner.gameObject.scene.name == "Plateau", "Saved Plateau has a scene-local spawner");
            Check(spawner.Enemies.Count == 1 && spawner.Enemies[0].maxAlive == 5 &&
                  spawner.Enemies[0].spawnInterval == 2f && spawner.SafeDistance == 3f,
                "Goblin configuration: 2 seconds / 5 alive / 3 units, no Cyclops entry");
            Vector2 savedAreaSize = spawner.SpawnArea.Size;
            Check(savedAreaSize == new Vector2(22f, 9f), "M9 editable interior area is 22 x 9 units");
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            player.enabled = false;
            FreezeEnemies();
            spawner.Spawned += Observe;
            int initial = spawner.CountAlive("Goblin");
            Check(initial == 0, "Plateau has no fixed initial Goblin; the first enemy uses the random spawner");
            var placed = Object.Instantiate(spawner.Enemies[0].prefab, new Vector3(5f, 0f, 0f), Quaternion.identity);
            Check(spawner.CountAlive("Goblin") == 1, "Externally placed Enemy_Basic is counted exactly once");
            Object.Destroy(placed.gameObject);
            yield return null;
            Check(spawner.CountAlive("Goblin") == 0, "Removing an externally placed enemy leaves no stale count");
            RestartClock();
            yield return new WaitForSeconds(1f);
            Check(Living().Length == initial, "No immediate spawn or initial burst");
            yield return new WaitForSeconds(1.15f);
            Check(Living().Length == initial + 1, "First interval adds exactly one enemy");
            report.firstSpawnPositions.Add(Living().Single().transform.position);
            for (int interval = 0; interval < 4 && Living().Length < 5; interval++)
                yield return new WaitForSeconds(2.1f);
            yield return new WaitForSeconds(4.2f);
            Check(Living().Length == 5 && spawner.CountAlive("Goblin") == 5, "Cap stays at five across extra intervals");
            Check(spawnTimes.Zip(spawnTimes.Skip(1), (a, b) => b - a).All(d => d >= 1.99f), "Observed natural spawns are at least two scaled seconds apart");

            RestartClock();
            var victims = Living().Take(2).ToArray();
            int exp = GameSession.Instance.TotalExperience, gold = GameSession.Instance.CurrentGold;
            int rewardExp = victims.Sum(e => e.GetComponent<EnemyReward>().Experience);
            int rewardGold = victims.Sum(e => e.GetComponent<EnemyReward>().Gold);
            foreach (var victim in victims)
            {
                victim.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
                victim.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
                victim.GetComponent<EnemyReward>().GrantOnce();
            }
            Check(spawner.CountAlive("Goblin") == 3, "Dying enemies release both slots immediately without negative counters");
            Check(GameSession.Instance.TotalExperience - exp == rewardExp && GameSession.Instance.CurrentGold - gold == rewardGold,
                "Death and repeated damage/reward calls grant EXP and gold exactly once");
            yield return null;
            Check(victims.All(v => v == null), "Dead enemy GameObjects are destroyed");
            yield return new WaitForSeconds(2.1f);
            Check(Living().Length == 4, "First death replacement adds one, not two");
            yield return new WaitForSeconds(2.1f);
            Check(Living().Length == 5, "Second death replacement waits another interval");

            RestartClock();
            var disabled = Living()[0]; disabled.gameObject.SetActive(false);
            yield return new WaitForSeconds(2.1f);
            Check(spawner.CountAlive("Goblin") == 5, "Disabled living enemies still reserve their slot");
            Object.Destroy(disabled.gameObject);
            yield return null;
            Check(spawner.CountAlive("Goblin") == 4, "External Destroy frees a slot without a death event");
            RestartClock();
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(2.3f);
            Check(Living().Length == 4, "timeScale zero prevents spawning during gameplay pause");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1f);
            Check(Living().Length == 4, "Pause accumulates no spawn debt");
            yield return new WaitForSeconds(1.15f);
            Check(Living().Length == 5, "Spawning resumes after the remaining scaled interval");

            spawner.enabled = false;
            foreach (var enemy in Living()) Object.Destroy(enemy.gameObject);
            yield return null;
            player.transform.position = new Vector3(-8f, -3f, 0f);
            Check(Valid(new Vector2(5f, 0f)), "Open interior location accepts the actual prefab footprint");
            Check(!Valid(new Vector2(-8f, -3f)) && !Valid(new Vector2(-5.4f, -3f)), "Player center and footprint inside safe zone are rejected");
            player.transform.position = new Vector3(5f, 0f, 0f);
            Check(!Valid(new Vector2(5f, 0f)) && Valid(new Vector2(-8f, -3f)), "Safe zone follows current player position");
            player.transform.position = new Vector3(-8f, -3f, 0f);
            Check(!Valid(new Vector2(11f, 0f)) && !Valid(new Vector2(12.2f, 0f)), "Whole enemy footprint must remain inside area");
            var areaSettings = new SerializedObject(spawner.SpawnArea);
            areaSettings.FindProperty("size").vector2Value = new Vector2(30f, 20f);
            areaSettings.ApplyModifiedPropertiesWithoutUndo();
            Check(!Valid(new Vector2(-11.7f, 0f)) && Valid(new Vector2(-11f, 0f)),
                "World boundary rejects collider-edge overlap even inside an enlarged spawn area");
            areaSettings.FindProperty("size").vector2Value = savedAreaSize;
            areaSettings.ApplyModifiedPropertiesWithoutUndo();
            Check(!Valid(new Vector2(0f, 3.7f)) && !Valid(new Vector2(0f, -3.7f)), "Portal and entrance approach clearances reject nearby spawns");

            var obstacle = new GameObject("M8_TestObstacle", typeof(BoxCollider2D));
            obstacle.layer = LayerMask.NameToLayer("World");
            obstacle.transform.position = new Vector3(5f, 0f, 0f);
            obstacle.GetComponent<BoxCollider2D>().size = Vector2.one;
            Check(!Valid(new Vector2(5f, 0f)) && !Valid(new Vector2(5.8f, 0f)), "World obstacle and edge-only footprint overlap are rejected");
            obstacle.GetComponent<BoxCollider2D>().isTrigger = true;
            bool previousTriggers = Physics2D.queriesHitTriggers; Physics2D.queriesHitTriggers = false;
            Check(!Valid(new Vector2(5f, 0f)), "Trigger blocking is independent of global queriesHitTriggers");
            Check(!Physics2D.queriesHitTriggers, "Spawn query restores the previous global trigger setting");
            Physics2D.queriesHitTriggers = previousTriggers;
            Object.Destroy(obstacle);
            var occupied = Object.Instantiate(spawner.Enemies[0].prefab, new Vector3(5f, 0f, 0f), Quaternion.identity);
            occupied.GetComponent<EnemyAI>().enabled = false; occupied.GetComponent<ContactDamage>().enabled = false;
            Check(!Valid(new Vector2(5f, 0f)), "Existing enemy collider blocks a new spawn");
            Object.Destroy(occupied.gameObject);
            yield return null;

            SetFloat("safeDistance", 100f); spawner.enabled = true;
            yield return new WaitForSeconds(2.1f);
            Check(Living().Length == 0 && spawner.LastAttemptCount == 20, "Impossible safe zone skips after exactly twenty attempts");
            yield return new WaitForSeconds(2.1f);
            Check(Living().Length == 0 && spawner.LastAttemptCount == 20, "Repeated failed intervals remain bounded without forced spawning");
            SetFloat("safeDistance", 3f); RestartClock();
            spawner.enabled = false;
            yield return new WaitForSeconds(2.2f);
            Check(Living().Length == 0, "Disabled spawner produces no enemies");
            spawner.enabled = true;
            yield return new WaitForSeconds(1f);
            Check(Living().Length == 0, "Re-enable starts a fresh interval");
            yield return new WaitForSeconds(1.15f);
            Check(Living().Length == 1, "Valid spawns recover after impossible-area and disable tests");

            GameSession.Instance.AddExperience(10000);
            Check(GameSession.Instance.TryLevelUpEnemies(), "Existing GameSession enemy-level purchase succeeds");
            yield return new WaitForSeconds(2.1f);
            var leveled = Living().First(e => e.GetComponent<EnemyProgression>().Level == GameSession.Instance.EnemyLevel);
            var health = leveled.GetComponent<EnemyHealth>(); var reward = leveled.GetComponent<EnemyReward>();
            var contact = leveled.GetComponent<ContactDamage>(); int level = GameSession.Instance.EnemyLevel;
            Check(health.CurrentHealth == health.MaxHealth && health.MaxHealth == GameSession.Instance.Progression.EnemyHealth(health.BaseMaxHealth, level) &&
                  contact.Damage == GameSession.Instance.Progression.EnemyDamage(contact.BaseDamage, level) &&
                  reward.Experience == GameSession.Instance.Progression.EnemyExperience(reward.BaseExperience, level) &&
                  reward.Gold == GameSession.Instance.Progression.EnemyGold(reward.BaseGold, level),
                "New enemies use current level, full fresh HP, scaled damage, EXP and gold");
            exp = GameSession.Instance.TotalExperience; gold = GameSession.Instance.CurrentGold;
            rewardExp = reward.Experience; rewardGold = reward.Gold;
            health.TakeDamage(int.MaxValue); reward.GrantOnce();
            Check(GameSession.Instance.TotalExperience - exp == rewardExp && GameSession.Instance.CurrentGold - gold == rewardGold,
                "Leveled enemy grants scaled rewards exactly once");

            spawner.enabled = false;
            var combatEnemy = Living().First();
            health = combatEnemy.GetComponent<EnemyHealth>();
            var ai = combatEnemy.GetComponent<EnemyAI>();
            var body = combatEnemy.GetComponent<Rigidbody2D>();
            combatEnemy.transform.position = player.transform.position + Vector3.right * 2.5f;
            Physics2D.SyncTransforms(); ai.enabled = true;
            yield return new WaitForSeconds(.15f);
            Check(body.linearVelocity.x < 0f, "Spawned enemy retains working chase AI");
            health.TakeDamage(1, Vector2.right);
            Check(body.linearVelocity.x > 0f, "Spawned enemy retains damage knockback");
            ai.enabled = false;
            int playerHp = player.GetComponent<PlayerHealth>().CurrentHealth;
            combatEnemy.GetComponent<ContactDamage>().enabled = true;
            combatEnemy.transform.position = player.transform.position;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.1f);
            Check(player.GetComponent<PlayerHealth>().CurrentHealth == playerHp - combatEnemy.GetComponent<ContactDamage>().Damage,
                "Spawned enemy retains contact damage with cooldown");
            combatEnemy.GetComponent<ContactDamage>().enabled = false;
            health.SetInitialMaxHealth(100);
            var attack = player.GetComponent<PlayerAttack>();
            var attackSettings = new SerializedObject(attack);
            var hitbox = (BoxCollider2D)attackSettings.FindProperty("hitbox").objectReferenceValue;
            combatEnemy.transform.position = hitbox.transform.TransformPoint(hitbox.offset);
            Physics2D.SyncTransforms(); int beforeHit = health.CurrentHealth;
            Check(attack.TryAttack() && health.CurrentHealth == beforeHit - attack.Damage,
                "Existing PlayerAttack hits spawned enemy through its real hitbox");
            var healthSettings = new SerializedObject(health);
            var smoke = (ParticleSystem)healthSettings.FindProperty("deathSmoke").objectReferenceValue;
            health.TakeDamage(int.MaxValue);
            Check(smoke != null && smoke.transform.parent == null && smoke.isPlaying,
                "Spawned enemy detaches and plays existing death smoke");
            yield return new WaitForSeconds(1f);
            Check(combatEnemy == null && smoke == null, "Enemy and death effect clean up without orphan objects");

            // Use real transition service and its loading screen for the round trip.
            player.enabled = true;
            var service = Object.FindAnyObjectByType<SceneTransitionService>();
            var portal = Object.FindObjectsByType<ScenePortal>().First(p => p.DestinationScene == "Village");
            Check(service.TryTransition("Village", portal.DestinationSpawnPointId, player), "Real Plateau-to-Village transition starts");
            yield return new WaitForSecondsRealtime(2.5f);
            Check(SceneManager.GetActiveScene().name == "Village" && !SceneTransitionService.IsTransitioning && spawner == null,
                "Plateau unload destroys spawner and loading completes in Village");
            yield return new WaitForSeconds(2.2f);
            Check(Object.FindObjectsByType<EnemySpawner>().Length == 0 &&
                  Object.FindObjectsByType<EnemySpawnIdentity>().Length == 0,
                "Village creates no enemies across a spawn interval");
            player = Object.FindAnyObjectByType<PlayerMovement>();
            portal = Object.FindObjectsByType<ScenePortal>().First(p => p.DestinationScene == "Plateau");
            Check(portal.TransitionService.TryTransition("Plateau", portal.DestinationSpawnPointId, player), "Real Village-to-Plateau transition starts");
            yield return new WaitForSecondsRealtime(2.5f);
            spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Check(spawner != null && !SceneTransitionService.IsTransitioning && spawner.CountAlive("Goblin") == 0,
                "Reloaded Plateau has no fixed Goblin and does not spawn during loading");
            FreezeEnemies(); spawner.Spawned += Observe;
            Object.FindAnyObjectByType<PlayerMovement>().enabled = false;
            yield return new WaitForSeconds(2.1f);
            Check(spawner.CountAlive("Goblin") == 1, "Reloaded Plateau resumes spawning on a fresh timer");
            report.firstSpawnPositions.Add(Living().Single().transform.position);
            Check(Vector2.Distance(report.firstSpawnPositions[0], report.firstSpawnPositions[1]) > .01f,
                "First Goblin positions differ between initial entry and Plateau reload");
            yield return SceneManager.LoadSceneAsync("Tower");
            yield return new WaitForSeconds(2.2f);
            Check(Object.FindObjectsByType<EnemySpawner>().Length == 0 &&
                  Object.FindObjectsByType<EnemySpawnIdentity>().Length == 0,
                "Tower creates no enemies across a spawn interval");
            Check(report.observedSpawns >= 8 && Time.frameCount > report.firstFrame + 100,
                "Real player loop advanced while observing multiple safe spawns");
        }
    }
}
