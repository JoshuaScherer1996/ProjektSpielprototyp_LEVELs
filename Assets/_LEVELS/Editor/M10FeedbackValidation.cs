using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levels.Core;
using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using Levels.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Integration checks against the saved prefabs and real combat, collision and spawn paths.
    /// All test overrides and temporary objects are restricted to Play mode.</summary>
    [InitializeOnLoad]
    public static class M10FeedbackValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public string startedUtc;
            public List<string> passed = new();
            public List<string> errors = new();
            public int firstFrame, lastFrame;
        }
        private const string Pending = "LEVELS.M10.Pending";
        public const string ResultPath = "Library/M10FeedbackValidation.json";
        private static Report report;
        private static Gamepad pad;
        static M10FeedbackValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("LEVEL's/M10/Run Feedback Play Mode Validation")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes first.");
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
            Application.logMessageReceived += CaptureError;
            GameSession.Instance.StartCoroutine(GuardedRun());
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + stack);
        }

        private static IEnumerator GuardedRun()
        {
            IEnumerator tests = Run();
            while (true)
            {
                object next;
                try { if (!tests.MoveNext()) break; next = tests.Current; }
                catch (Exception e) { report.errors.Add(e.ToString()); break; }
                File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
                yield return next;
            }
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            Time.timeScale = 1;
            report.lastFrame = Time.frameCount;
            report.status = report.errors.Count == 0 ? "passed" : "failed";
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            Debug.Log($"M10 feedback validation: {report.status}, {report.passed.Count} checks. {ResultPath}");
            EditorApplication.isPlaying = false;
        }

        private static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException(description);
            report.passed.Add(description);
        }
        private static FloatingDamageNumber[] Numbers() => Object.FindObjectsByType<FloatingDamageNumber>();
        private static TMP_Text Label(FloatingDamageNumber number) => number.GetComponentInChildren<TMP_Text>();
        private static RectTransform Fill(EnemyHealth enemy) => enemy.GetComponentInChildren<EnemyHealthBar>(true)
            .transform.Find("Inset/HPFill").GetComponent<RectTransform>();
        private static void Freeze(EnemySpawnIdentity enemy)
        {
            enemy.GetComponent<EnemyAI>().enabled = false;
            enemy.GetComponent<ContactDamage>().enabled = false;
            // Unity delivers collision messages even to disabled MonoBehaviours.
            // Keep preparation hits out of the contact test by isolating player collisions too.
            enemy.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Player");
        }
        private static void Move(Component actor, Vector2 position)
        {
            var body = actor.GetComponent<Rigidbody2D>();
            body.position = position; body.linearVelocity = Vector2.zero;
            actor.transform.position = position;
            Physics2D.SyncTransforms();
        }

        private static IEnumerator Run()
        {
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            player.enabled = false;
            var playerHealth = player.GetComponent<PlayerHealth>();
            var attack = player.GetComponent<PlayerAttack>();
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            // This regression suite exercises the original Goblin feedback population.
            // M11EnemyValidation covers both types together with the real saved category limits.
            foreach (var category in spawner.Enemies)
                if (category.category != "Goblin") category.maxAlive = 0;
            spawner.Spawned += Freeze;
            yield return new WaitForSeconds(12.2f);
            var enemies = Object.FindObjectsByType<EnemyHealth>();
            Check(enemies.Length == 5 && spawner.CountAlive("Goblin") == 5, "Natural spawner reaches and keeps the five-Goblin cap");
            var bars = Object.FindObjectsByType<EnemyHealthBar>();
            Check(bars.Length == 5 && enemies.All(e => bars.Count(b => b.Health == e && b.transform.IsChildOf(e.transform)) == 1),
                "Every spawned Goblin owns exactly one correctly bound nested HP-bar instance");
            Check(enemies.All(e => Fill(e).anchorMax.x == 1f), "All five fresh bars display full HP");
            var a = enemies[0]; var b = enemies[1];
            Vector3 offset = a.GetComponentInChildren<EnemyHealthBar>().transform.position - a.transform.position;
            Move(a, new Vector2(-3f, 0));
            Check(Vector3.Distance(a.GetComponentInChildren<EnemyHealthBar>().transform.position, a.transform.position + offset) < .001f,
                "HP bar follows only its owning enemy when it moves");
            spawner.enabled = false;
            Move(player, Vector2.zero);
            foreach (var enemy in enemies) Move(enemy, new Vector2(5 + Array.IndexOf(enemies, enemy), 2));
            var session = GameSession.Instance;
            session.AddExperience(10000);
            Check(session.TryLevelUpEnemies(), "Enemy-level purchase succeeds through GameSession");
            var leveled = Object.Instantiate(spawner.Enemies[0].prefab, new Vector3(-5, 0, 0), Quaternion.identity);
            Freeze(leveled);
            yield return null;
            var leveledHealth = leveled.GetComponent<EnemyHealth>();
            int expectedMax = session.Progression.EnemyHealth(leveledHealth.BaseMaxHealth, session.EnemyLevel);
            Check(leveledHealth.MaxHealth == expectedMax && expectedMax > leveledHealth.BaseMaxHealth && Fill(leveledHealth).anchorMax.x == 1f,
                "New leveled instance displays its own scaled maximum as full HP");
            leveledHealth.TakeDamage(1);
            Check(Mathf.Abs(Fill(leveledHealth).anchorMax.x - (float)(expectedMax - 1) / expectedMax) < .001f && Fill(a).anchorMax.x == 1f,
                "Scaled HP fraction updates without changing another Goblin's bar");
            Object.Destroy(leveled.gameObject);
            yield return new WaitForSecondsRealtime(.9f);
            Check(Numbers().Length == 0, "Temporary floating numbers expire after the configured default 0.8 seconds");

            pad = InputSystem.AddDevice<Gamepad>("M10_TestPad");
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", pad);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up });
            yield return null;
            Check(Vector2.Dot(attack.Facing, Vector2.up) > .99f, "Attack facing still follows the controller's right stick");
            for (int i = 0; i < 4; i++)
                Check(session.TryLevelUpPlayer(), "Player-level purchase " + (i + 1) + " succeeds through GameSession");
            Check(attack.Damage == 3, "Real player progression supplies three damage for the overkill test");
            Check(attack.TryAttack() && Numbers().Length == 0, "A real attack that misses creates no damage number");
            yield return new WaitForSeconds(.35f);
            var hitbox = (BoxCollider2D)new SerializedObject(attack).FindProperty("hitbox").objectReferenceValue;
            Vector2 center = hitbox.transform.TransformPoint(hitbox.offset);
            Move(a, center + Vector2.left * .37f);
            Move(b, center + Vector2.right * .37f);
            a.SetInitialMaxHealth(1);
            b.SetInitialMaxHealth(10);
            // Multiple colliders belonging to A still represent a single target per sword swing.
            var extra = new GameObject("M10_ExtraCollider", typeof(BoxCollider2D));
            extra.transform.SetParent(a.transform, false);
            extra.GetComponent<BoxCollider2D>().isTrigger = true;
            extra.GetComponent<BoxCollider2D>().size = Vector2.one * .2f;
            yield return new WaitForSecondsRealtime(.9f);
            Physics2D.SyncTransforms();
            int exp = session.TotalExperience, gold = session.CurrentGold;
            var reward = a.GetComponent<EnemyReward>();
            int rewardExp = reward.Experience, rewardGold = reward.Gold;
            int bBefore = b.CurrentHealth;
            bool swung = attack.TryAttack();
            Check(swung && a.CurrentHealth == 0 && b.CurrentHealth == bBefore - 3,
                $"One real sword swing hits two targets with independent applied HP losses (swing={swung}, A={a.CurrentHealth}, B={b.CurrentHealth}/{bBefore}, damage={attack.Damage})");
            var numbers = Numbers();
            Check(numbers.Length == 2 && numbers.Count(n => Label(n).text == "1") == 1 && numbers.Count(n => Label(n).text == "3") == 1,
                "Exactly one number per target: overkill displays 1 and other target displays 3, including multi-collider target");
            var one = numbers.Single(n => Label(n).text == "1");
            var three = numbers.Single(n => Label(n).text == "3");
            Check(Mathf.Abs(one.transform.position.x - a.transform.position.x) < .01f &&
                  Mathf.Abs(three.transform.position.x - b.transform.position.x) < .01f,
                "Simultaneous numbers originate above their own distinct target positions");
            Color golden = new Color(1f, .79f, .16f);
            Check(numbers.All(n => Label(n).color == golden && Label(n).font != null && Label(n).fontSharedMaterial.GetFloat("_OutlineWidth") > 0),
                "Enemy damage uses golden TMP retro digits with a dark outline");
            Check(!a.GetComponentInChildren<Canvas>(true).enabled && Mathf.Abs(Fill(b).anchorMax.x - .7f) < .001f,
                "Lethal bar hides immediately; surviving bar shows 7/10");
            Check(!a.TakeDamage(3) && !a.TakeDamage(0) && !a.TakeDamage(-1) && !b.TakeDamage(0) && Numbers().Length == 2,
                "Dead targets and nonpositive damage create no numbers");
            reward.GrantOnce();
            Check(session.TotalExperience - exp == rewardExp && session.CurrentGold - gold == rewardGold,
                "Lethal feedback preserves exactly-once EXP and gold rewards");
            Check(!attack.TryAttack() && Numbers().Length == 2 && b.CurrentHealth == bBefore - 3,
                "Attack cooldown rejects repeat swing without duplicate damage or numbers");
            var smoke = (ParticleSystem)new SerializedObject(a).FindProperty("deathSmoke").objectReferenceValue;
            Check(smoke != null && smoke.transform.parent == null && smoke.isPlaying, "Lethal hit still detaches and plays death smoke");
            Vector3 numberOrigin = one.transform.position;
            var shortLife = new SerializedObject(three); shortLife.FindProperty("duration").floatValue = .2f; shortLife.ApplyModifiedPropertiesWithoutUndo();
            yield return null;
            Check(a == null && Object.FindObjectsByType<EnemyHealthBar>().Length == 4 && one != null,
                "Death destroys its bar with the enemy while the lethal number remains alive");
            yield return new WaitForSecondsRealtime(.25f);
            Check(three == null && one != null && one.transform.position.y > numberOrigin.y,
                "Configurable shorter duration expires independently; default number rises upward");
            yield return new WaitForSecondsRealtime(.65f);
            Check(Numbers().Length == 0 && smoke == null, "Numbers and death smoke leave no orphan objects");
            Object.Destroy(extra);

            // Re-enable must neither retain stale health nor add duplicate event subscriptions.
            b.gameObject.SetActive(false); b.gameObject.SetActive(true);
            int before = b.CurrentHealth;
            b.TakeDamage(1);
            Check(Numbers().Length == 1 && Label(Numbers()[0]).text == "1" && Mathf.Abs(Fill(b).anchorMax.x - (before - 1f) / b.MaxHealth) < .001f,
                "Disable/re-enable refreshes own bar and subscribes to damage exactly once");
            yield return new WaitForSecondsRealtime(.9f);

            // Exercise ContactDamage through a physical collision, not a direct player-health call.
            Move(b, Vector2.zero);
            Vector3 contactHitPosition = Vector3.zero;
            Action<int> observeContact = _ => contactHitPosition = player.transform.position;
            playerHealth.DamageApplied += observeContact;
            b.GetComponent<Collider2D>().excludeLayers &= ~LayerMask.GetMask("Player");
            b.GetComponent<ContactDamage>().enabled = true;
            int playerBefore = playerHealth.CurrentHealth;
            yield return new WaitForSeconds(.1f);
            b.GetComponent<ContactDamage>().enabled = false;
            b.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Player");
            playerHealth.DamageApplied -= observeContact;
            Check(playerHealth.CurrentHealth == playerBefore - b.GetComponent<ContactDamage>().Damage && Numbers().Length == 1,
                "Actual enemy contact applies player damage and emits exactly one number within cooldown");
            var red = Numbers().Single();
            Check(Label(red).color == new Color(1f, .28f, .12f) && Label(red).text == (playerBefore - playerHealth.CurrentHealth).ToString() &&
                  Mathf.Abs(red.transform.position.x - contactHitPosition.x) < .01f,
                $"Player damage uses red-orange digits at the player with the actual HP loss (value={Label(red).text}, loss={playerBefore - playerHealth.CurrentHealth}, x={red.transform.position.x}, hitX={contactHitPosition.x}, color={Label(red).color})");
            Move(b, new Vector2(1, 1));
            b.TakeDamage(1);
            var follow = Object.FindAnyObjectByType<CameraFollow>(); follow.enabled = false;
            Camera.main.transform.position = new Vector3(0, .5f, -10); Camera.main.orthographicSize = 3f;
            ScreenCapture.CaptureScreenshot("Library/M10Feedback.png");
            yield return null; yield return null;
            follow.enabled = true;
            yield return new WaitForSecondsRealtime(.9f);
            Check(Numbers().Length == 0, "Simultaneous red-orange and gold feedback both clean up");

            // Session healing/max-HP changes must not be mistaken for combat damage.
            session.UpdatePlayerHealth(playerHealth.MaxHealth, playerHealth.MaxHealth);
            playerHealth.SetMaxHealth(playerHealth.MaxHealth + 1);
            Check(Numbers().Length == 0, "Session healing and max-health changes never create damage numbers");
            for (int i = 0; i < 5; i++) playerHealth.TakeDamage(1);
            Check(Numbers().Length == 5 && Numbers().Select(n => n.transform.position.x).Distinct().Count() == 5,
                "Five applied hits on one player create five separate horizontal number positions");
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.9f);
            Check(Numbers().Length == 0, "Feedback lifetime and cleanup also finish while gameplay time is paused");
            Time.timeScale = 1;
            int playerRemaining = playerHealth.CurrentHealth;
            playerHealth.TakeDamage(int.MaxValue);
            Check(Numbers().Length == 1 && Label(Numbers()[0]).text == playerRemaining.ToString() && !playerHealth.TakeDamage(1),
                "Lethal player overkill reports only remaining HP and rejects repeat damage");
            yield return null;
            Check(player == null && Numbers().Length == 1, "Player's lethal number survives player destruction");
            yield return SceneManager.LoadSceneAsync("Village");
            Check(Numbers().Length == 0 && Object.FindObjectsByType<EnemyHealthBar>().Length == 0,
                "Scene unload removes all old feedback, bars and target references");
            Check(Object.FindAnyObjectByType<PlayerHealth>().GetComponent<DamageFeedback>() != null,
                "New scene player inherits feedback through the shared Player prefab");
            Check(Time.frameCount > report.firstFrame + 100, "Real Unity player loop advanced throughout integration checks");
        }
    }
}
