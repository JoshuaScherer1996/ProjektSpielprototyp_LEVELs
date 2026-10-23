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
using Levels.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Repeatable integration tests using the saved M11 assets and actual player/physics/input loops.
    /// Temporary actors, money, input devices and overrides exist only in Play mode.</summary>
    [InitializeOnLoad]
    public static class M11EnemyValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public string startedUtc = DateTime.UtcNow.ToString("o");
            public List<string> passed = new();
            public List<string> errors = new();
            public int firstFrame, lastFrame;
            public int goblinSpawns, cyclopsSpawns;
            public float minimumPlayerClearance = float.MaxValue;
        }
        public const string ResultPath = "Library/M11EnemyValidation.json";
        private const string Pending = "LEVELS.M11.Pending";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Report report;
        private static EnemySpawner spawner;
        private static Gamepad pad;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static readonly Dictionary<string, float> lastSpawn = new();
        static M11EnemyValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            report.passed.Add(description);
        }

        [MenuItem("LEVEL's/M11/Validate Saved Enemy Assets")]
        public static void ValidateSavedAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            report = new Report();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Check(EditorBuildSettings.scenes.First(s => s.enabled).path == LevelsPlayModeSetup.MainMenuPath,
                    "Build starts in MainMenu (M12)");
                Check(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == LevelsPlayModeSetup.MainMenuPath,
                    "Normal Editor Play starts in MainMenu regardless of the open scene (M12)");
                Check(AssetDatabase.AssetPathToGUID(M11EnemySetup.GoblinPath) == M11EnemySetup.GoblinGuid,
                    "Goblin rename preserves original Unity GUID");
                var goblin = AssetDatabase.LoadAssetAtPath<GameObject>(M11EnemySetup.GoblinPath);
                var cyclops = AssetDatabase.LoadAssetAtPath<GameObject>(M11EnemySetup.CyclopsPath);
                Check(goblin != null && cyclops != null && !PrefabUtility.IsPartOfVariantPrefab(cyclops),
                    "Both editable enemy prefabs exist; Cyclops is an independent prefab");
                foreach (var root in new[] { goblin, cyclops })
                {
                    Check(root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                        root.name + ": no missing scripts");
                    Check(root.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic &&
                          root.GetComponent<Rigidbody2D>().collisionDetectionMode == CollisionDetectionMode2D.Continuous &&
                          !root.GetComponent<BoxCollider2D>().isTrigger && root.layer == LayerMask.NameToLayer("Enemy") && !root.CompareTag("Player"),
                        root.name + ": solid continuous Rigidbody2D on Enemy layer");
                    Check(root.GetComponent<EnemyAI>() != null && root.GetComponent<EnemyHealth>() != null &&
                          root.GetComponent<ContactDamage>() != null && root.GetComponent<EnemyReward>() != null &&
                          root.GetComponent<EnemyProgression>() != null && root.GetComponent<DamageFeedback>() != null &&
                          root.GetComponentsInChildren<EnemyHealthBar>(true).Length == 1,
                        root.name + ": shared combat, progression, feedback and exactly one HP bar");
                }
                Check(goblin.GetComponent<EnemyHealth>().BaseMaxHealth == 3 && goblin.GetComponent<ContactDamage>().BaseDamage == 1 &&
                      goblin.GetComponent<EnemyReward>().BaseExperience == 10 && goblin.GetComponent<EnemyReward>().BaseGold == 5,
                    "Goblin base values: 3 HP / 1 damage / 10 EXP / 5 gold");
                Check(cyclops.GetComponent<EnemyHealth>().BaseMaxHealth == 8 && cyclops.GetComponent<ContactDamage>().BaseDamage == 3 &&
                      cyclops.GetComponent<EnemyReward>().BaseExperience == 25 && cyclops.GetComponent<EnemyReward>().BaseGold == 15,
                    "Cyclops base values: 8 HP / 3 damage / 25 EXP / 15 gold");
                Check(goblin.GetComponent<BoxCollider2D>().size == new Vector2(.7f, .8f) &&
                      goblin.GetComponent<SpriteRenderer>().color == new Color(.9f, .17f, .18f) &&
                      !new SerializedObject(goblin.GetComponent<EnemyAI>()).FindProperty("persistentAggro").boolValue,
                    "Goblin retains red rectangle, collider dimensions and range-limited AI");
                Check(cyclops.GetComponent<BoxCollider2D>().size.x > goblin.GetComponent<BoxCollider2D>().size.x &&
                      cyclops.GetComponent<BoxCollider2D>().size.y > goblin.GetComponent<BoxCollider2D>().size.y &&
                      cyclops.GetComponent<SpriteRenderer>().color == new Color(.48f, .27f, .12f), "Cyclops has a larger brown body and matching collider");
                var eye = cyclops.transform.Find("Eye").GetComponent<SpriteRenderer>();
                var pupil = cyclops.transform.Find("Pupil").GetComponent<SpriteRenderer>();
                Check(eye.color == Color.white && eye.sprite != null && pupil.sprite != null &&
                      pupil.color.maxColorComponent <= 1 && pupil.color.r < .2f &&
                      eye.transform.localScale.x > pupil.transform.localScale.x && eye.sortingOrder > cyclops.GetComponent<SpriteRenderer>().sortingOrder &&
                      pupil.sortingOrder > eye.sortingOrder, "Cyclops owns a large white SpriteRenderer eye and smaller dark pupil above its body");
                var settings = Resources.Load<ProgressionSettings>("LevelsProgression");
                var tuning = Object.Instantiate(settings);
                try
                {
                    int goblinHP = tuning.EnemyHealth(3, 3);
                    tuning.cyclopsHealthPerLevel = 99;
                    Check(tuning.EnemyHealth(3, 3) == goblinHP && tuning.EnemyHealth(8, 3, EnemyType.Cyclops) == 206,
                        "Cyclops progression tuning cannot change Goblin scaling");
                    Check(tuning.EnemyHealth(int.MaxValue, int.MaxValue, EnemyType.Cyclops) == int.MaxValue &&
                          tuning.EnemyGold(int.MaxValue, int.MaxValue, EnemyType.Cyclops) == int.MaxValue &&
                          tuning.EnemyCost(int.MaxValue, EnemyType.Cyclops) > 0, "Progression saturates without integer wrap or negative costs");
                }
                finally { Object.DestroyImmediate(tuning); }
                var menu = AssetDatabase.LoadAssetAtPath<GameObject>(M11EnemySetup.Root + "Prefabs/UI/LevelingMenu.prefab");
                var buttons = menu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Check(buttons.Length == 4 && new SerializedObject(menu.GetComponent<LevelingMenu>()).FindProperty("cyclopsButton").objectReferenceValue != null &&
                      buttons.All(b => b.navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit && b.navigation.selectOnUp != null && b.navigation.selectOnDown != null),
                    "Leveling prefab has Player/Goblin/Cyclops/Close buttons, references and explicit cyclic navigation");
                foreach (string name in new[] { "Village", "Plateau", "Tower" })
                {
                    var scene = EditorSceneManager.OpenScene(M11EnemySetup.Root + "Scenes/" + name + ".unity");
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                    Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), name + ": saved scene has no missing scripts");
                    foreach (var component in all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c != null))
                    {
                        var iterator = new SerializedObject(component).GetIterator();
                        while (iterator.NextVisible(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null && !iterator.objectReferenceEntityIdValue.Equals(default(EntityId)))
                                throw new InvalidOperationException(name + ": broken reference " + component.name + "." + iterator.propertyPath);
                    }
                    Check(true, name + ": no broken serialized component references");
                    if (name != "Plateau") continue;
                    var s = all.Select(t => t.GetComponent<EnemySpawner>()).Single(x => x != null);
                    Check(s.Enemies.Count == 2 && s.Enemies[0].category == "Goblin" && s.Enemies[0].maxAlive == 5 &&
                          s.Enemies[1].category == "Cyclops" && s.Enemies[1].maxAlive == 2 && s.Enemies[0].spawnInterval == 2 &&
                          s.Enemies[1].spawnInterval == 3 && s.Enemies.All(e => e.prefab.Category == e.category),
                        "Saved Plateau uses two matching categories: 5 Goblins at 2 seconds + 2 Cyclops at 3 seconds");
                }
                report.status = "passed";
            }
            catch (Exception e) { report.status = "failed"; report.errors.Add(e.ToString()); throw; }
            finally
            {
                File.WriteAllText("Library/M11SavedAssetsValidation.json", JsonUtility.ToJson(report, true));
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("LEVEL's/M11/Run Enemy Play Mode Validation")]
        public static void Start()
        {
            ValidateSavedAssets();
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(M8SpawnSetup.PlateauPath);
            LevelsPlayModeSetup.UseValidationScene(M8SpawnSetup.PlateauPath);
            EditorApplication.isPlaying = true;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            report = new Report { firstFrame = Time.frameCount };
            lastSpawn.Clear();
            Application.logMessageReceived += CaptureError;
            GameSession.Instance.StartCoroutine(GuardedRun());
        }
        private static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack);
        }
        private static IEnumerator GuardedRun()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object next = null; bool moved;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception e) { report.errors.Add(e.ToString()); break; }
                if (!moved) { stack.Pop(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true)); yield return next;
            }
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1;
            report.lastFrame = Time.frameCount; report.status = report.errors.Count == 0 ? "passed" : "failed";
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            Debug.Log("M11 validation: " + report.status + ", " + report.passed.Count + " checks. " + ResultPath);
            EditorApplication.isPlaying = false;
        }

        private static PlayerMovement Player() => Object.FindAnyObjectByType<PlayerMovement>();
        private static EnemySpawnIdentity[] Living(string category = null) => Object.FindObjectsByType<EnemySpawnIdentity>()
            .Where(e => e.IsAlive && (category == null || e.Category == category)).ToArray();
        private static void Move(Component actor, Vector2 position)
        {
            var body = actor.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
            actor.transform.position = position; Physics2D.SyncTransforms();
        }
        private static void Freeze(EnemySpawnIdentity enemy)
        {
            enemy.GetComponent<EnemyAI>().enabled = false;
            enemy.GetComponent<ContactDamage>().enabled = false;
            enemy.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Player");
        }
        private static void Observe(EnemySpawnIdentity enemy)
        {
            Freeze(enemy);
            float interval = spawner.Enemies.Single(e => e.category == enemy.Category).spawnInterval;
            if (lastSpawn.TryGetValue(enemy.Category, out float before) && Time.time - before + .025f < interval)
                throw new InvalidOperationException("Category spawned faster than its interval: " + enemy.Category);
            lastSpawn[enemy.Category] = Time.time;
            if (enemy.Category == "Goblin") report.goblinSpawns++; else report.cyclopsSpawns++;
            Check(spawner.CountAlive("Goblin") <= 5 && spawner.CountAlive("Cyclops") <= 2 && Living().Length <= 7, "Spawn stays within separate 5/2/7 limits");
            var bounds = enemy.GetComponent<Collider2D>().bounds; bounds.Expand(new Vector3(.2f, .2f, 0));
            float distance = Vector2.Distance(bounds.ClosestPoint(Player().transform.position), Player().transform.position);
            report.minimumPlayerClearance = Mathf.Min(report.minimumPlayerClearance, distance);
            Check(distance >= spawner.SafeDistance && spawner.SpawnArea.Contains(bounds), enemy.Category + ": actual padded collider stays in area and outside Player safe zone");
            bool previous = Physics2D.queriesHitTriggers;
            var hits = new List<Collider2D>();
            try
            {
                Physics2D.queriesHitTriggers = true;
                var filter = new ContactFilter2D { useTriggers = true }; filter.SetLayerMask(LayerMask.GetMask("Default", "World", "Player", "Enemy"));
                Physics2D.OverlapBox(bounds.center, bounds.size, 0, filter, hits);
            }
            finally { Physics2D.queriesHitTriggers = previous; }
            Check(hits.All(c => c.transform.IsChildOf(enemy.transform)), enemy.Category + ": no overlap with obstacles, portals, Player or other enemies");
        }
        private static Bounds Footprint(EnemySpawnIdentity prefab)
        {
            var args = new object[] { prefab, default(Bounds) };
            if (!(bool)typeof(EnemySpawner).GetMethod("TryGetFootprint", Private).Invoke(spawner, args)) throw new InvalidOperationException("Unsupported collider footprint");
            return (Bounds)args[1];
        }
        private static bool Valid(EnemySpawnIdentity prefab, Vector2 position)
        {
            Bounds footprint = Footprint(prefab); footprint.center += (Vector3)position;
            Physics2D.SyncTransforms();
            return (bool)typeof(EnemySpawner).GetMethod("IsPositionValid", Private).Invoke(spawner, new object[] { footprint,
                (Vector2)Player().transform.position, Object.FindObjectsByType<ScenePortal>(FindObjectsInactive.Include), Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include) });
        }
        private static void RestartClocks() { lastSpawn.Clear(); spawner.enabled = false; spawner.enabled = true; }
        private static RectTransform Fill(EnemyHealth health) => health.GetComponentInChildren<EnemyHealthBar>(true).transform.Find("Inset/HPFill").GetComponent<RectTransform>();
        private static FloatingDamageNumber[] Numbers() => Object.FindObjectsByType<FloatingDamageNumber>();
        private static TMP_Text Digits(FloatingDamageNumber number) => number.GetComponentInChildren<TMP_Text>();
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (value is Vector2 v) p.vector2Value = v;
            else if (value is float f) p.floatValue = f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator Run()
        {
            yield return null;
            var player = Player(); player.enabled = false;
            spawner = Object.FindAnyObjectByType<EnemySpawner>(); spawner.Spawned += Observe;
            var session = GameSession.Instance;
            Check(session.PlayerLevel == 1 && session.GoblinLevel == 1 && session.CyclopsLevel == 1 && session.EnemyLevel == session.GoblinLevel,
                "New session starts at three independent level-1 values; legacy EnemyLevel aliases Goblin");
            Check(!session.TryLevelUpGoblin() && !session.TryLevelUpCyclops() && !session.TryLevelUpPlayer() && session.CurrentExperience == 0,
                "Insufficient EXP rejects all three transactions without negative EXP");
            yield return new WaitForSeconds(2.2f);
            Check(Living("Goblin").Length == 1 && Living("Cyclops").Length == 0, "At 2.2 seconds only the first Goblin has spawned");
            yield return new WaitForSeconds(1f);
            Check(Living("Goblin").Length == 1 && Living("Cyclops").Length == 1, "At 3.2 seconds the first Cyclops joins, offset from Goblin");
            yield return new WaitForSeconds(7.2f);
            Check(Living("Goblin").Length == 5 && Living("Cyclops").Length == 2 && Living().Length == 7, "Natural independent clocks reach exactly 5 Goblins and 2 Cyclops");
            yield return new WaitForSeconds(2.1f);
            Check(report.goblinSpawns == 5 && report.cyclopsSpawns == 2, "Full categories remain capped across further intervals");
            Check(Living().Select(e => e.transform.position).Distinct().Count() == 7, "All seven spawn positions are distinct");
            foreach (var enemy in Living())
            {
                Check(enemy.GetComponent<EnemyProgression>().Level == 1 && Fill(enemy.GetComponent<EnemyHealth>()).anchorMax.x == 1 &&
                      enemy.GetComponentInChildren<EnemyHealthBar>().Health == enemy.GetComponent<EnemyHealth>(), enemy.Category + ": level-1 stats and full HP bar bound to its own health");
            }
            spawner.enabled = false;
            yield return ContactAndRewards();
            spawner.Enemies[0].spawnInterval = .4f; spawner.Enemies[1].spawnInterval = .8f;
            RestartClocks();
            yield return new WaitForSeconds(.16f);
            Check(spawner.CountAlive("Goblin") == 4 && spawner.CountAlive("Cyclops") == 1, "Deaths free only the matching slots with no instant respawn");
            yield return new WaitForSeconds(.36f);
            Check(spawner.CountAlive("Goblin") == 5 && spawner.CountAlive("Cyclops") == 1, "Goblin replacement uses its own shorter clock while Cyclops waits");
            yield return new WaitForSeconds(.4f);
            Check(spawner.CountAlive("Goblin") == 5 && spawner.CountAlive("Cyclops") == 2, "Cyclops replacement uses its own clock without exceeding either cap");
            spawner.enabled = false;
            foreach (var enemy in Living()) Object.Destroy(enemy.gameObject);
            yield return null;
            yield return SafeSpawnTests();
            yield return AITests();
            yield return SwordInputTests();
            yield return ScalingTests();
            yield return MenuAndSessionTests();
            Check(Time.frameCount > report.firstFrame + 100, "Actual Unity player loop advanced throughout integration checks");
        }

        private static IEnumerator ContactAndRewards()
        {
            var session = GameSession.Instance;
            var player = Player(); var health = player.GetComponent<PlayerHealth>();
            Move(player, Vector2.zero);
            // Keep other actors away from the contact experiment.
            int index = 0; foreach (var e in Living()) Move(e, new Vector2(-9 + index++ * 2.5f, -3));
            foreach (string category in new[] { "Goblin", "Cyclops" })
            {
                var enemy = Living(category)[0]; var contact = enemy.GetComponent<ContactDamage>();
                session.UpdatePlayerHealth(health.MaxHealth, health.MaxHealth);
                Move(enemy, Vector2.zero); contact.enabled = true;
                enemy.GetComponent<Collider2D>().excludeLayers &= ~LayerMask.GetMask("Player");
                int before = health.CurrentHealth;
                yield return new WaitForSeconds(.12f);
                Check(before - health.CurrentHealth == (category == "Goblin" ? 1 : 3), category + ": actual physical contact applies correct base damage once");
                yield return new WaitForSeconds(.12f);
                Check(before - health.CurrentHealth == contact.Damage, category + ": contact cooldown rejects duplicate damage");
                Freeze(enemy); Move(enemy, new Vector2(-5, 0));
                yield return new WaitForSecondsRealtime(.9f);
                var h = enemy.GetComponent<EnemyHealth>(); h.TakeDamage(1, Vector2.right);
                Check(Mathf.Abs(Fill(h).anchorMax.x - (float)(h.MaxHealth - 1) / h.MaxHealth) < .001f && Numbers().Length == 1 && Digits(Numbers()[0]).text == "1",
                    category + ": real damage updates own HP fraction and golden damage digits (HP=" + h.CurrentHealth + "/" + h.MaxHealth +
                    ", fill=" + Fill(h).anchorMax.x + ", numbers=" + Numbers().Length + ", text=" + string.Join(",", Numbers().Select(n => Digits(n).text)) + ")");
                int exp = session.TotalExperience, gold = session.CurrentGold;
                var reward = enemy.GetComponent<EnemyReward>(); int rewardExp = reward.Experience, rewardGold = reward.Gold;
                h.TakeDamage(int.MaxValue); reward.GrantOnce();
                Check(!h.TakeDamage(1) && session.TotalExperience - exp == rewardExp && session.CurrentGold - gold == rewardGold &&
                      rewardExp == (category == "Goblin" ? 10 : 25) && rewardGold == (category == "Goblin" ? 5 : 15),
                    category + ": death and repeat callbacks grant correct EXP/gold exactly once");
                var smoke = (ParticleSystem)new SerializedObject(h).FindProperty("deathSmoke").objectReferenceValue;
                Check(!h.GetComponentInChildren<Canvas>(true).enabled && smoke != null && smoke.transform.parent == null && smoke.isPlaying,
                    category + ": lethal hit hides bar and plays detached death smoke");
                yield return null;
                Check(enemy == null, category + ": dead enemy and owned health bar are destroyed");
                yield return new WaitForSecondsRealtime(.9f);
                Check(Numbers().Length == 0 && smoke == null, category + ": damage numbers and death smoke clean up");
            }
            session.UpdatePlayerHealth(health.MaxHealth, health.MaxHealth);
        }

        private static IEnumerator SafeSpawnTests()
        {
            var goblin = spawner.Enemies[0].prefab; var cyclops = spawner.Enemies[1].prefab;
            Move(Player(), new Vector2(0, -4));
            Check(Footprint(cyclops).size.x > Footprint(goblin).size.x && Footprint(cyclops).size.y > Footprint(goblin).size.y,
                "Safe spawning derives the larger Cyclops footprint from its real collider");
            foreach (var prefab in new[] { goblin, cyclops })
            {
                Check(!Valid(prefab, Player().transform.position), prefab.Category + ": Player safe zone rejects spawn");
                Check(!Valid(prefab, new Vector2(12, 0)) && !Valid(prefab, new Vector2(-12, 0)), prefab.Category + ": east/west cliffs and outside area reject spawn");
                foreach (var portal in Object.FindObjectsByType<ScenePortal>())
                    Check(!Valid(prefab, portal.transform.position), prefab.Category + ": scene portal rejects spawn");
                foreach (var entrance in Object.FindObjectsByType<SpawnPoint>())
                    Check(!Valid(prefab, entrance.transform.position), prefab.Category + ": entrance clearance rejects spawn");
            }
            var wall = new GameObject("M11_TemporaryNarrowClearance", typeof(BoxCollider2D));
            wall.layer = LayerMask.NameToLayer("World"); wall.transform.position = new Vector3(-5.35f, 0, 0);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(.2f, 2);
            Check(Valid(goblin, new Vector2(-6, 0)) && !Valid(cyclops, new Vector2(-6, 0)),
                "One position fits padded Goblin but rejects larger Cyclops beside a solid wall");
            Object.Destroy(wall); yield return null;
            var obstacle = Object.Instantiate(goblin, new Vector3(-6, 0), Quaternion.identity); Freeze(obstacle);
            yield return null;
            Check(!Valid(goblin, new Vector2(-6, 0)) && !Valid(cyclops, new Vector2(-6, 0)), "Both categories reject overlapping another living enemy");
            Object.Destroy(obstacle.gameObject); yield return null;
            Vector2 areaSize = spawner.SpawnArea.Size;
            Set(spawner.SpawnArea, "size", new Vector2(.01f, .01f));
            foreach (var entry in spawner.Enemies) entry.spawnInterval = .1f;
            RestartClocks();
            yield return new WaitForSeconds(.35f);
            Check(Living().Length == 0 && spawner.LastAttemptCount == 20, "Fully invalid spawn area performs bounded attempts and never forces a spawn");
            spawner.enabled = false; Set(spawner.SpawnArea, "size", areaSize);
            foreach (var entry in spawner.Enemies) entry.spawnInterval = entry.category == "Cyclops" ? 3f : 2f;
        }

        private static IEnumerator AITests()
        {
            Move(Player(), new Vector2(9, 0));
            var goblin = Object.Instantiate(spawner.Enemies[0].prefab, Vector3.zero, Quaternion.identity);
            var cyclops = Object.Instantiate(spawner.Enemies[1].prefab, new Vector3(0, 2), Quaternion.identity);
            foreach (var e in new[] { goblin, cyclops }) e.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Player");
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            var ai = cyclops.GetComponent<EnemyAI>(); var body = cyclops.GetComponent<Rigidbody2D>();
            Check(!ai.HasAggro && body.linearVelocity == Vector2.zero, "Cyclops waits before first detection outside configurable radius");
            Move(Player(), new Vector2(2, 1));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(ai.HasAggro && Mathf.Abs(body.linearVelocity.magnitude - M11EnemySetup.CyclopsTargetSpeed) < .01f &&
                  ai.MoveSpeed < goblin.GetComponent<EnemyAI>().MoveSpeed && ai.MoveSpeed < Player().MoveSpeed,
                "Cyclops moves at 1.4 units per second, slower than Goblin and Player");
            Check(goblin.GetComponent<Rigidbody2D>().linearVelocity.magnitude > 0, "Goblin still chases inside its existing detection radius");
            Move(Player(), new Vector2(10, 0));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(ai.HasAggro && body.linearVelocity.magnitude > 0 && goblin.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
                "Cyclops continues outside initial radius while Goblin stops");
            ai.ApplyKnockback(Vector2.left);
            yield return new WaitForFixedUpdate();
            Check(body.linearVelocity.x < 0 && ai.HasAggro, "Cyclops knockback interrupts movement without clearing persistent aggro");
            yield return new WaitForSeconds(.2f);
            Check(body.linearVelocity.x > 0, "Cyclops resumes pursuit after knockback");
            ai.enabled = false;
            Move(cyclops, new Vector2(10.8f, 0));
            for (int i = 0; i < 35; i++) { body.linearVelocity = Vector2.right * 4; yield return new WaitForFixedUpdate(); }
            Check(cyclops.transform.position.x < 11.5f && cyclops.GetComponent<Collider2D>().IsTouchingLayers(LayerMask.GetMask("World")),
                "Large Cyclops collider is physically blocked by east cliff");
            foreach (int sign in new[] { -1, 1 })
            {
                Move(cyclops, new Vector2(0, sign * 6));
                for (int i = 0; i < 30; i++) { body.linearVelocity = Vector2.up * sign * 4; yield return new WaitForFixedUpdate(); }
                Check(Mathf.Abs(cyclops.transform.position.y) < 7 && SceneManager.GetActiveScene().name == "Plateau" && !SceneTransitionService.IsTransitioning,
                    "Cyclops cannot leave Plateau or activate " + (sign > 0 ? "north" : "south") + " portal");
            }
            Move(cyclops, Vector2.zero); Move(Player(), new Vector2(2, 0)); ai.enabled = true;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            cyclops.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
            typeof(EnemyAI).GetMethod("FixedUpdate", Private).Invoke(ai, null);
            Check(body.linearVelocity == Vector2.zero && !ai.HasAggro, "Cyclops death immediately ends pursuit before deferred destruction");
            Object.Destroy(goblin.gameObject); yield return null;
        }

        private static IEnumerator SwordInputTests()
        {
            yield return new WaitForSecondsRealtime(.9f);
            pad = InputSystem.AddDevice<Gamepad>("M11_TestPad");
            var player = Player(); Move(player, Vector2.zero);
            var input = player.GetComponent<PlayerInput>(); input.SwitchCurrentControlScheme("Gamepad", pad);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up });
            yield return null; yield return null;
            var attack = player.GetComponent<PlayerAttack>();
            var hitbox = (BoxCollider2D)new SerializedObject(attack).FindProperty("hitbox").objectReferenceValue;
            Vector2 center = hitbox.transform.TransformPoint(hitbox.offset);
            var goblin = Object.Instantiate(spawner.Enemies[0].prefab, center + Vector2.left * .38f, Quaternion.identity);
            var cyclops = Object.Instantiate(spawner.Enemies[1].prefab, center + Vector2.right * .38f, Quaternion.identity);
            Freeze(goblin); Freeze(cyclops);
            // Isolate sword hitbox queries from enemy/enemy collision separation during arrangement.
            goblin.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Enemy");
            cyclops.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Enemy");
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up }.WithButton(GamepadButton.RightShoulder));
            yield return null; yield return null;
            Check(goblin.GetComponent<EnemyHealth>().CurrentHealth == 2 && cyclops.GetComponent<EnemyHealth>().CurrentHealth == 7 &&
                  Numbers().Length == 2 && Numbers().All(n => Digits(n).text == "1"),
                "Real R1/RB input damages Goblin and Cyclops once each and creates separate damage numbers");
            Check(!attack.TryAttack() && goblin.GetComponent<EnemyHealth>().CurrentHealth == 2 && cyclops.GetComponent<EnemyHealth>().CurrentHealth == 7,
                "Existing attack cooldown rejects a second swing against both types");
            Pad(); Object.Destroy(goblin.gameObject); Object.Destroy(cyclops.gameObject); yield return null;
        }

        private static IEnumerator ScalingTests()
        {
            var session = GameSession.Instance; session.AddExperience(10000);
            int exp = session.CurrentExperience, playerLevel = session.PlayerLevel;
            int first = session.NextGoblinLevelExperience;
            Check(session.TryLevelUpEnemies() && session.CurrentExperience == exp - first && session.GoblinLevel == 2 && session.CyclopsLevel == 1,
                "Legacy enemy purchase upgrades only Goblin and debits once");
            exp = session.CurrentExperience; first = session.NextGoblinLevelExperience;
            Check(session.TryLevelUpGoblin() && session.GoblinLevel == 3 && session.CyclopsLevel == 1 && session.PlayerLevel == playerLevel && session.CurrentExperience == exp - first,
                "Goblin reaches level 3 independently while Cyclops remains level 1");
            bool reentrant = true;
            Action observer = () => reentrant = session.TryLevelUpCyclops();
            session.Changed += observer;
            exp = session.CurrentExperience; int cost = session.NextPlayerLevelExperience;
            session.TryLevelUpPlayer(); session.Changed -= observer;
            Check(!reentrant && session.CurrentExperience == exp - cost && session.PlayerLevel == playerLevel + 1 && session.CyclopsLevel == 1,
                "Player transaction debits once and rejects reentrant enemy purchase");
            RestartClocks(); yield return new WaitForSeconds(3.2f); spawner.enabled = false;
            foreach (var enemy in Living())
            {
                var type = enemy.GetComponent<EnemyProgression>(); var h = enemy.GetComponent<EnemyHealth>();
                var damage = enemy.GetComponent<ContactDamage>(); var reward = enemy.GetComponent<EnemyReward>();
                int level = enemy.Category == "Goblin" ? 3 : 1;
                Check(type.Level == level && h.MaxHealth == session.Progression.EnemyHealth(h.BaseMaxHealth, level, type.Type) &&
                      damage.Damage == session.Progression.EnemyDamage(damage.BaseDamage, level, type.Type) &&
                      reward.Experience == session.Progression.EnemyExperience(reward.BaseExperience, level, type.Type) &&
                      reward.Gold == session.Progression.EnemyGold(reward.BaseGold, level, type.Type),
                    enemy.Category + ": natural spawn uses exactly one own-type scaling pass for HP/damage/EXP/gold");
            }
            exp = session.CurrentExperience; cost = session.NextCyclopsLevelExperience;
            Check(session.TryLevelUpCyclops() && session.CyclopsLevel == 2 && session.GoblinLevel == 3 && session.CurrentExperience == exp - cost,
                "Cyclops purchase changes only its own level with one debit");
            Check(Living("Cyclops").Single().GetComponent<EnemyProgression>().Level == 1, "Purchases never retroactively multiply existing enemies");
            foreach (var enemy in Living()) Object.Destroy(enemy.gameObject);
            yield return null;
            var next = Object.Instantiate(spawner.Enemies[1].prefab, Vector3.zero, Quaternion.identity); Freeze(next);
            yield return null;
            var settings = session.Progression;
            Check(next.GetComponent<EnemyProgression>().Level == 2 && next.GetComponent<EnemyHealth>().MaxHealth == settings.EnemyHealth(8, 2, EnemyType.Cyclops) &&
                  next.GetComponent<ContactDamage>().Damage == settings.EnemyDamage(3, 2, EnemyType.Cyclops) &&
                  next.GetComponent<EnemyReward>().Experience == settings.EnemyExperience(25, 2, EnemyType.Cyclops) &&
                  next.GetComponent<EnemyReward>().Gold == settings.EnemyGold(15, 2, EnemyType.Cyclops), "Next Cyclops applies its independent level-2 HP/damage/rewards");
            next.gameObject.SetActive(false); next.gameObject.SetActive(true); yield return null;
            Check(next.GetComponent<EnemyHealth>().MaxHealth == settings.EnemyHealth(8, 2, EnemyType.Cyclops), "Disable/re-enable never applies scaling twice");
            Object.Destroy(next.gameObject); yield return null;
        }

        private static void Pad(params GamepadButton[] buttons)
        {
            var state = new GamepadState(); foreach (var b in buttons) state = state.WithButton(b);
            InputSystem.QueueStateEvent(pad, state);
        }
        private static IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.1f);
        }
        private static IEnumerator MenuAndSessionTests()
        {
            var session = GameSession.Instance;
            int g = session.GoblinLevel, c = session.CyclopsLevel, p = session.PlayerLevel, exp = session.CurrentExperience;
            var portal = Object.FindObjectsByType<ScenePortal>().Single(x => x.DestinationScene == "Village");
            Player().enabled = true;
            Check(portal.TransitionService.TryTransition("Village", portal.DestinationSpawnPointId, Player()), "Actual transition service begins Plateau-to-Village return");
            Check(SceneTransitionService.IsTransitioning && portal.TransitionService.Screen.IsVisible, "Loading overlay is visible during M11 session transition");
            float end = Time.realtimeSinceStartup + 15;
            while (SceneTransitionService.IsTransitioning && Time.realtimeSinceStartup < end) yield return null;
            Check(SceneManager.GetActiveScene().name == "Village" && !SceneTransitionService.IsTransitioning && GameSession.Instance == session &&
                  session.GoblinLevel == g && session.CyclopsLevel == c && session.PlayerLevel == p && session.CurrentExperience == exp,
                "Same session, all three levels and exact EXP survive actual scene transition");
            keyboard = InputSystem.AddDevice<Keyboard>("M11_TestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("M11_TestMouse");
            var player = Player(); var menu = Object.FindAnyObjectByType<LevelingMenu>();
            var input = player.GetComponent<PlayerInput>(); input.SwitchCurrentControlScheme("Gamepad", pad);
            var station = Object.FindAnyObjectByType<LevelingStation>();
            var blocker = (Collider2D)new SerializedObject(station).FindProperty("blockingCollider").objectReferenceValue;
            Move(player, new Vector2(blocker.bounds.center.x, blocker.bounds.min.y - player.GetComponent<Collider2D>().bounds.extents.y - .03f));
            Pad(GamepadButton.South); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return new WaitForSecondsRealtime(.12f);
            Check(menu.IsOpen && !player.enabled && !player.GetComponent<PlayerAttack>().enabled && session.PlayerLevel == p,
                "Gamepad-A opens the real leveling well, locks controls and makes no accidental purchase");
            yield return null; yield return null;
            string details = menu.transform.Find("Panel/Card/Details").GetComponent<UnityEngine.UI.Text>().text;
            Check(details.Contains("Goblin Lv 3") && details.Contains("Cyclops Lv 2") && details.Contains("Available EXP") && details.Contains("Cyclops upgrade:"),
                "Menu displays all three current levels, spendable EXP and separate costs");
            exp = session.CurrentExperience; int cost = session.NextGoblinLevelExperience;
            Pad(GamepadButton.DpadDown); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return new WaitForSecondsRealtime(.12f);
            Check(EventSystem.current.currentSelectedGameObject.name == "EnemyUpgrade", "Gamepad navigation selects Goblin purchase");
            Pad(GamepadButton.South); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return new WaitForSecondsRealtime(.12f);
            Check(session.GoblinLevel == g + 1 && session.CyclopsLevel == c && session.PlayerLevel == p && session.CurrentExperience == exp - cost,
                "Gamepad Submit buys only Goblin with exactly one EXP debit");
            Pad(GamepadButton.DpadDown); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return new WaitForSecondsRealtime(.12f);
            exp = session.CurrentExperience; cost = session.NextCyclopsLevelExperience;
            Pad(GamepadButton.South); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return new WaitForSecondsRealtime(.12f);
            Check(session.CyclopsLevel == c + 1 && session.GoblinLevel == g + 1 && session.CurrentExperience == exp - cost, "Gamepad independently buys Cyclops");
            Pad(GamepadButton.East); yield return new WaitForSecondsRealtime(.12f); Pad(); yield return null;
            Check(!menu.IsOpen && player.enabled && player.GetComponent<PlayerAttack>().enabled, "Gamepad Cancel closes menu and restores movement/combat");
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            yield return PressKey(Key.E);
            Check(menu.IsOpen && !player.enabled, "E opens the real leveling well using the existing Interact action");
            exp = session.CurrentExperience; cost = session.NextPlayerLevelExperience;
            yield return PressKey(Key.Enter);
            Check(session.PlayerLevel == p + 1 && session.CurrentExperience == exp - cost, "Keyboard Submit purchases Player independently");
            yield return PressKey(Key.DownArrow); yield return PressKey(Key.DownArrow);
            Check(EventSystem.current.currentSelectedGameObject.name == "CyclopsUpgrade", "Keyboard navigation reaches Cyclops category");
            exp = session.CurrentExperience; cost = session.NextCyclopsLevelExperience;
            yield return PressKey(Key.Enter);
            Check(session.CyclopsLevel == c + 2 && session.CurrentExperience == exp - cost, "Keyboard Submit purchases Cyclops exactly once");
            // Click the actual GraphicRaycaster target using Input System pointer events.
            var button = menu.transform.Find("Panel/Card/EnemyUpgrade").GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases(); Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, button.position);
            exp = session.CurrentExperience; cost = session.NextGoblinLevelExperience;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); yield return new WaitForSecondsRealtime(.1f);
            Check(session.GoblinLevel == g + 2 && session.CurrentExperience == exp - cost, "Mouse click purchases Goblin through actual UI raycast exactly once");
            session.TrySpendExperience(session.CurrentExperience); yield return null;
            int before = session.CyclopsLevel; menu.BuyCyclopsLevel(); menu.BuyCyclopsLevel();
            Check(session.CurrentExperience == 0 && session.CyclopsLevel == before && menu.transform.Find("Panel/Card/Feedback").GetComponent<UnityEngine.UI.Text>().text.Contains("Not enough EXP"),
                "Insufficient-EXP UI feedback and duplicate callbacks never spend or level");
            ScreenCapture.CaptureScreenshot("Library/M11_LevelingMenu.png"); yield return null; yield return null;
            yield return PressKey(Key.Escape);
            Check(!menu.IsOpen && player.enabled, "Keyboard Cancel restores Player controls");
            var shop = Object.FindAnyObjectByType<ShopTransactions>(); var health = player.GetComponent<PlayerHealth>();
            session.AddGold(100); health.TakeDamage(1);
            int gold = session.CurrentGold;
            Check(shop.TryPurchase(ShopOffer.Healing, health) == ShopPurchaseResult.Success && health.CurrentHealth == health.MaxHealth && session.CurrentGold == gold - shop.HealingPrice,
                "Shop healing still works through real transaction after separate enemy leveling");
            int maximum = health.MaxHealth; gold = session.CurrentGold;
            Check(shop.TryPurchase(ShopOffer.MaxHealth, health) == ShopPurchaseResult.Success && health.MaxHealth == maximum + 1 && session.CurrentGold == gold - shop.MaxHealthPrice,
                "Shop max-HP purchase still debits once and stacks with Player progression");
            // Preserve all level values through a second scene; enemies use them upon re-entry.
            g = session.GoblinLevel; c = session.CyclopsLevel; p = session.PlayerLevel;
            yield return SceneManager.LoadSceneAsync("Plateau"); yield return null;
            spawner = Object.FindAnyObjectByType<EnemySpawner>(); spawner.Spawned += Observe;
            Player().enabled = false;
            Check(session.GoblinLevel == g && session.CyclopsLevel == c && session.PlayerLevel == p && session.CurrentExperience == 0, "All three levels remain on Plateau re-entry with zero EXP");
            yield return new WaitForSeconds(3.2f);
            Check(Living().Length == 2 && Living().All(e => e.GetComponent<EnemyProgression>().Level == session.GetEnemyLevel(e.GetComponent<EnemyProgression>().Type)),
                "Re-entered Plateau spawns one of each type at persisted independent levels");
            // Capture editable body/eye and actual HP/damage feedback together.
            spawner.enabled = false; Move(Player(), new Vector2(0, -2));
            Move(Living("Goblin").Single(), new Vector2(-1.5f, 0)); Move(Living("Cyclops").Single(), new Vector2(1.5f, 0));
            foreach (var enemy in Living()) enemy.GetComponent<EnemyHealth>().TakeDamage(1);
            var follow = Object.FindAnyObjectByType<CameraFollow>(); follow.enabled = false;
            Camera.main.transform.position = new Vector3(0, 0, -10); Camera.main.orthographicSize = 4;
            ScreenCapture.CaptureScreenshot("Library/M11_Enemies.png"); yield return null; yield return null;
            var cyclops = Living("Cyclops").Single(); var ai = cyclops.GetComponent<EnemyAI>();
            Move(cyclops, Vector2.zero); Move(Player(), new Vector2(2, 0)); ai.enabled = true;
            yield return new WaitForSeconds(.2f);
            Player().GetComponent<PlayerHealth>().TakeDamage(int.MaxValue);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(!ai.HasAggro && cyclops.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "Player death ends persistent Cyclops pursuit");
        }
    }
}
