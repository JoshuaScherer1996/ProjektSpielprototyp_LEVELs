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
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Real saved-scene integration checks. Synthetic devices exist only during this explicit Play test.</summary>
    [InitializeOnLoad]
    public static class M12FlowValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public List<string> passed = new();
            public List<string> errors = new();
            public int firstFrame, lastFrame;
        }
        public const string ResultPath = "Library/M12FlowValidation.json";
        private const string Pending = "LEVELS.M12.Pending";
        private const string RestoreBackground = "LEVELS.M12.RestoreBackground";
        private const string PreviousBackground = "LEVELS.M12.PreviousBackground";
        private static Report report;
        private static Gamepad pad;
        private static Keyboard keyboard;
        private static Mouse mouse;
        static M12FlowValidation() => EditorApplication.playModeStateChanged += OnPlayMode;
        private static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException(description);
            report.passed.Add(description);
        }

        [MenuItem("LEVEL's/M12/Validate Saved Menu Assets")]
        public static void ValidateSavedAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            report = new Report();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
                var scenes = profile != null && profile.overrideGlobalScenes ? profile.scenes : EditorBuildSettings.scenes;
                Check(scenes.First(s => s.enabled).path == M12MenuSetup.MenuPath, "MainMenu is first enabled scene in the actual active build configuration");
                Check(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == M12MenuSetup.MenuPath, "Normal Editor Play starts in MainMenu");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(M12MenuSetup.FontPath);
                Check(font != null && "LEVEL'sGAME OVERRETRYBACK TO MAIN MENUPLAYEXIT".All(c => font.HasCharacter(c)), "Embedded pixel font covers every menu label");
                foreach (string name in new[] { "MainMenu", "Village", "Plateau", "Tower" })
                {
                    var scene = EditorSceneManager.OpenScene(M12MenuSetup.Root + "Scenes/" + name + ".unity");
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                    Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), name + ": no missing scripts");
                    foreach (var component in all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c != null))
                    {
                        var iterator = new SerializedObject(component).GetIterator();
                        while (iterator.NextVisible(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null && !iterator.objectReferenceEntityIdValue.Equals(default(EntityId)))
                                throw new InvalidOperationException(name + ": broken reference " + component.name + "." + iterator.propertyPath);
                    }
                    Check(true, name + ": no broken serialized component references");
                    Check(Object.FindObjectsByType<GameFlow>().Length == 1 && Object.FindObjectsByType<SceneTransitionService>().Length == 1 &&
                          Object.FindObjectsByType<EventSystem>().Length == 1, name + ": exactly one flow, transition service and EventSystem");
                    Check(Object.FindObjectsByType<PlayerMovement>().Length == (name == "MainMenu" ? 0 : 1), name + ": correct saved Player count");
                    var focus = Object.FindObjectsByType<MenuFocus>(FindObjectsInactive.Include).Single();
                    var so = new SerializedObject(focus);
                    Check(so.FindProperty("inputModule").objectReferenceValue != null && so.FindProperty("firstButton").objectReferenceValue != null,
                        name + ": UI selection and shared Input System module are assigned");
                    var canvas = focus.GetComponentInParent<Canvas>();
                    Check(canvas != null && canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null && canvas.sortingOrder == (name == "MainMenu" ? 100 : 200),
                        name + ": menu has Canvas, raycaster and foreground sorting");
                    Check(Object.FindObjectsByType<GameOverUI>(FindObjectsInactive.Include).Length == (name == "MainMenu" ? 0 : 1), name + ": scene-owned GameOverUI count");
                    if (name == "Village") Check(Object.FindObjectsByType<SpawnPoint>().Count(p => p.Id == "NewRun") == 1, "Village contains one saved NewRun spawn point");
                }
                report.status = "passed";
            }
            catch (Exception e) { report.status = "failed"; report.errors.Add(e.ToString()); throw; }
            finally
            {
                File.WriteAllText("Library/M12SavedAssetsValidation.json", JsonUtility.ToJson(report, true));
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("LEVEL's/M12/Run Flow Play Mode Validation")]
        public static void Start()
        {
            ValidateSavedAssets();
            SessionState.SetBool(PreviousBackground, Application.runInBackground);
            SessionState.SetBool(RestoreBackground, true);
            Application.runInBackground = true;
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(M12MenuSetup.MenuPath);
            LevelsPlayModeSetup.UseValidationScene(M12MenuSetup.MenuPath);
            EditorApplication.isPlaying = true;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreBackground, false))
            {
                Application.runInBackground = SessionState.GetBool(PreviousBackground, false);
                SessionState.EraseBool(RestoreBackground);
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            report = new Report { firstFrame = Time.frameCount };
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
            foreach (var device in new InputDevice[] { pad, keyboard, mouse }) if (device != null && device.added) InputSystem.RemoveDevice(device);
            Time.timeScale = 1;
            report.lastFrame = Time.frameCount; report.status = report.errors.Count == 0 ? "passed" : "failed";
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            Debug.Log("M12 validation: " + report.status + ", " + report.passed.Count + " checks. " + ResultPath);
            EditorApplication.isPlaying = false;
        }

        private static PlayerMovement Player() => Object.FindAnyObjectByType<PlayerMovement>();
        private static void Unique(int players)
        {
            Check(Object.FindObjectsByType<GameSession>(FindObjectsInactive.Include).Length == 1 &&
                  Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include).Length == players &&
                  Object.FindObjectsByType<GameFlow>().Length == 1 && Object.FindObjectsByType<EventSystem>().Length == 1,
                SceneManager.GetActiveScene().name + ": one session, flow, EventSystem and expected Player count " + players);
        }
        private static void Fresh()
        {
            var s = GameSession.Instance; var p = Player(); var h = p.GetComponent<PlayerHealth>();
            Check(GameFlow.State == RunState.Playing && GameFlow.IsGameplayActive && p.enabled && p.GetComponent<PlayerAttack>().enabled,
                "Fresh Village run immediately releases movement and combat locks");
            Check(s.PlayerLevel == 1 && s.GoblinLevel == 1 && s.CyclopsLevel == 1 && s.CurrentExperience == 0 && s.TotalExperience == 0 &&
                  s.CurrentGold == 0 && s.ShopMaxHealthBonus == 0, "Fresh run resets all three levels, current/total EXP, gold and shop upgrades");
            Check(h.CurrentHealth == h.BaseMaxHealth && h.MaxHealth == h.BaseMaxHealth && s.PlayerHealth == h.CurrentHealth && s.PlayerMaxHealth == h.MaxHealth &&
                  p.GetComponent<PlayerAttack>().Damage == p.GetComponent<PlayerAttack>().BaseDamage, "Fresh run resets current/max HP and old attack/stat bonuses");
            Check(Object.FindObjectsByType<EnemySpawnIdentity>(FindObjectsInactive.Include).Length == 0 && Object.FindObjectsByType<EnemySpawner>().Length == 0,
                "Retry/Play Village has no retained enemies, spawner or spawn counters");
            Unique(1);
        }
        private static void Move(Component actor, Vector2 position)
        {
            var body = actor.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
            actor.transform.position = position; Physics2D.SyncTransforms();
        }
        private static void Freeze(EnemySpawnIdentity enemy)
        {
            enemy.GetComponent<EnemyAI>().enabled = false; enemy.GetComponent<ContactDamage>().enabled = false;
            enemy.GetComponent<Collider2D>().excludeLayers |= LayerMask.GetMask("Player");
        }
        private static IEnumerator Loaded(string name)
        {
            float end = Time.realtimeSinceStartup + 15;
            while ((SceneTransitionService.IsTransitioning || SceneManager.GetActiveScene().name != name) && Time.realtimeSinceStartup < end) yield return null;
            yield return null; yield return null;
            Check(SceneManager.GetActiveScene().name == name && !SceneTransitionService.IsTransitioning, "Async loading completed: " + name);
        }
        private static void PadState(Vector2 stick, params GamepadButton[] buttons)
        {
            var state = new GamepadState { leftStick = stick }; foreach (var b in buttons) state = state.WithButton(b);
            InputSystem.QueueStateEvent(pad, state);
        }
        private static IEnumerator PadPress(GamepadButton button)
        {
            PadState(Vector2.zero, button); yield return new WaitForSecondsRealtime(.12f);
            PadState(Vector2.zero); yield return new WaitForSecondsRealtime(.12f);
        }
        private static IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.12f);
        }
        private static IEnumerator Click(string name)
        {
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b => b.name == name);
            Canvas.ForceUpdateCanvases(); var screen = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            if (name != "Retry")
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen + Vector2.right * 30 }); yield return new WaitForSecondsRealtime(.1f);
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); yield return new WaitForSecondsRealtime(.1f);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            Check(hits.Any(h => h.gameObject == button.gameObject) && Object.FindAnyObjectByType<InputSystemUIInputModule>().enabled,
                name + ": real mouse raycast reaches active button and UI input module");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }); yield return new WaitForSecondsRealtime(.1f);
            Check(SceneTransitionService.IsTransitioning, name + ": mouse click starts exactly one async run transition");
            if (name == "Retry") Check(true, "Stationary mouse can click RETRY after shared UI module is re-enabled");
        }
        private static IEnumerator Capture(string name)
        {
            ScreenCapture.CaptureScreenshot("Library/M12_" + name + ".png"); yield return null; yield return null;
        }
        private static IEnumerator Hover(string name)
        {
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b => b.name == name);
            Canvas.ForceUpdateCanvases(); Vector2 position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.15f);
            Check(EventSystem.current.currentSelectedGameObject == button.gameObject, "Mouse hover selects only " + name);
            foreach (var other in button.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                Color actual = other.targetGraphic.canvasRenderer.GetColor();
                Color expected = other == button ? other.colors.selectedColor : other.colors.normalColor;
                Check(Vector4.Distance(actual, expected) < .02f, name + " hover: " + other.name + " has the correct rendered highlight color");
            }
        }

        private static IEnumerator ReturnWithoutLoadingScreen()
        {
            bool overlaySeen = false;
            float started = Time.realtimeSinceStartup;
            PadState(Vector2.zero, GamepadButton.South);
            do
            {
                overlaySeen |= Object.FindObjectsByType<LoadingScreen>(FindObjectsInactive.Include).Any(s => s.IsVisible);
                if (Time.realtimeSinceStartup - started > .12f) PadState(Vector2.zero);
                yield return null;
            }
            while ((Time.realtimeSinceStartup - started < .15f || SceneTransitionService.IsTransitioning || SceneManager.GetActiveScene().name != "MainMenu") &&
                   Time.realtimeSinceStartup - started < 10);
            PadState(Vector2.zero);
            Check(!overlaySeen, "BACK TO MAIN MENU never displays a loading overlay in source or destination");
            yield return Loaded("MainMenu");
        }
        private static void Boost()
        {
            var session = GameSession.Instance; session.AddExperience(10000); session.AddGold(1000);
            Check(session.TryLevelUpPlayer() && session.TryLevelUpPlayer() && session.TryLevelUpGoblin() && session.TryLevelUpCyclops(), "Prepare independent Player/Goblin/Cyclops upgrades through real transactions");
            Check(Object.FindAnyObjectByType<ShopTransactions>().TryPurchase(ShopOffer.MaxHealth, Player().GetComponent<PlayerHealth>()) == ShopPurchaseResult.Success &&
                  session.ShopMaxHealthBonus == 1 && session.PlayerMaxHealth > Player().GetComponent<PlayerHealth>().BaseMaxHealth,
                "Prepare real shop max-HP upgrade plus progression health/damage bonus");
        }

        private static IEnumerator Run()
        {
            yield return null; yield return null;
            pad = InputSystem.AddDevice<Gamepad>("M12_TestPad"); keyboard = InputSystem.AddDevice<Keyboard>("M12_TestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("M12_TestMouse");
            yield return new WaitForSecondsRealtime(.2f);
            Check(GameFlow.State == RunState.MainMenu && !GameFlow.IsGameplayActive && Time.timeScale == 1, "Game boots into MainMenu with gameplay disabled and timeScale 1"); Unique(0);
            Check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.name == "Play",
                "MainMenu initially highlights PLAY for keyboard and gamepad");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) });
            yield return new WaitForSecondsRealtime(.15f);
            Check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.name == "Play",
                "Moving the mouse over empty background preserves the initial PLAY selection");
            yield return Hover("Play"); yield return Hover("Exit"); yield return Capture("MainMenu_HoverExit");
            yield return KeyPress(Key.UpArrow);
            Check(EventSystem.current.currentSelectedGameObject.name == "Play", "Keyboard takes selection back from mouse hover");
            yield return PadPress(GamepadButton.DpadDown);
            Check(EventSystem.current.currentSelectedGameObject.name == "Exit", "Gamepad navigation selects EXIT");
            var selected = EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.Button>();
            Check(selected.colors.selectedColor != selected.colors.normalColor, "Active button has a visibly different selection color");
            yield return PadPress(GamepadButton.DpadUp); yield return Capture("MainMenu");
            double requestedAt = Time.realtimeSinceStartupAsDouble;
            PadState(Vector2.right, GamepadButton.South, GamepadButton.RightShoulder); yield return null; yield return null;
            Check(SceneTransitionService.IsTransitioning && Object.FindAnyObjectByType<SceneTransitionService>().Screen.IsVisible && !GameFlow.IsGameplayActive,
                "Gamepad Submit on PLAY starts existing loading overlay and blocks gameplay");
            double minimumEnd = (double)typeof(SceneTransitionService).GetField("minimumEndTime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(Math.Abs(minimumEnd - requestedAt - 1) < .1, "PLAY configures a one-second loading minimum");
            yield return Loaded("Village"); Fresh();
            Check(Time.realtimeSinceStartupAsDouble - requestedAt >= .95, "PLAY keeps its loading overlay for approximately one second before releasing gameplay");
            var player = Player(); player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", pad);
            var spawn = Object.FindObjectsByType<SpawnPoint>().Single(p => p.Id == "NewRun");
            Check(Vector2.Distance(player.transform.position, spawn.transform.position) < .6f, "PLAY positions Player at saved NewRun spawn before held-stick movement");
            var slash = (SpriteRenderer)new SerializedObject(player.GetComponent<PlayerAttack>()).FindProperty("slashRenderer").objectReferenceValue;
            Check(!slash.enabled, "Holding R1/RB during menu/loading never carries an accidental attack into gameplay");
            Vector2 before = player.transform.position;
            yield return new WaitForSecondsRealtime(.25f);
            Check(player.transform.position.x > before.x + .2f, "Continuously held analog stick moves without requiring a new input edge after loading");
            PadState(Vector2.zero); yield return null;
            Move(player, spawn.transform.position);
            Boost();
            var session = GameSession.Instance; int experience = session.CurrentExperience, gold = session.CurrentGold;
            var portal = Object.FindObjectsByType<ScenePortal>().Single(p => p.DestinationScene == "Plateau");
            Check(portal.TransitionService.TryTransition("Plateau", portal.DestinationSpawnPointId, player), "Existing Village-to-Plateau transition still works");
            Check(!player.enabled && !player.GetComponent<PlayerAttack>().TryAttack() && !player.GetComponent<PlayerHealth>().TakeDamage(1),
                "Loading lock blocks movement, attack and incoming damage");
            yield return Loaded("Plateau"); Unique(1);
            Check(GameSession.Instance == session && session.PlayerLevel == 3 && session.GoblinLevel == 2 && session.CyclopsLevel == 2 && session.CurrentExperience == experience && session.CurrentGold == gold,
                "Ordinary scene transitions preserve the current session and independent progression");
            var spawner = Object.FindAnyObjectByType<EnemySpawner>(); spawner.Spawned += Freeze;
            yield return new WaitForSecondsRealtime(3.3f);
            Check(spawner.CountAlive("Goblin") >= 1 && spawner.CountAlive("Cyclops") >= 1, "Normal Plateau timers spawn both enemy categories");
            player = Player(); Move(player, Vector2.zero); player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", pad);
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up }); yield return null; yield return null;
            var attack = player.GetComponent<PlayerAttack>();
            var hitbox = (BoxCollider2D)new SerializedObject(attack).FindProperty("hitbox").objectReferenceValue;
            var target = Object.Instantiate(spawner.Enemies[0].prefab, hitbox.transform.TransformPoint(hitbox.offset), Quaternion.identity); Freeze(target);
            yield return null; yield return null; // EnemyProgression applies the session level in Start.
            var enemyHealth = target.GetComponent<EnemyHealth>(); int hp = enemyHealth.CurrentHealth, damageEvents = 0;
            enemyHealth.DamageApplied += _ => damageEvents++;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up }.WithButton(GamepadButton.RightShoulder)); yield return null; yield return null;
            Check(damageEvents == 1 && enemyHealth.CurrentHealth == hp - attack.Damage && !attack.TryAttack(),
                $"Actual R1/RB attacks once, uses correct stats and keeps cooldown without duplicate callbacks (events {damageEvents}, HP {enemyHealth.CurrentHealth}/{hp}, damage {attack.Damage})");
            PadState(Vector2.zero); yield return new WaitForSecondsRealtime(.2f);
            player.GetComponent<PlayerHealth>().TakeDamage(player.GetComponent<PlayerHealth>().CurrentHealth - 1);
            target.GetComponent<Collider2D>().excludeLayers &= ~LayerMask.GetMask("Player"); target.GetComponent<ContactDamage>().enabled = true;
            Move(target, player.transform.position); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Check(GameFlow.State == RunState.GameOver && Object.FindAnyObjectByType<GameOverUI>().IsVisible && Player() == null,
                "Real enemy collision kills Player and GAME OVER survives Player destruction"); Unique(0);
            int alive = Object.FindObjectsByType<EnemySpawnIdentity>().Length; experience = session.CurrentExperience; gold = session.CurrentGold;
            Check(!spawner.enabled && typeof(EnemySpawner).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawner) == null &&
                  Object.FindObjectsByType<EnemyAI>().All(a => !a.enabled) && Object.FindObjectsByType<ContactDamage>().All(d => !d.enabled),
                "Death disables spawner, clears timers and stops every enemy AI/contact damage component");
            Check(!enemyHealth.TakeDamage(1000), "Active enemies reject further damage during Game Over");
            target.GetComponent<EnemyReward>().GrantOnce(); session.AddExperience(100); session.AddGold(100);
            Check(!session.TryLevelUpPlayer() && !session.TryLevelUpGoblin() && !session.TryLevelUpCyclops(), "Game Over rejects progression transactions");
            yield return new WaitForSecondsRealtime(3.4f);
            Check(Object.FindObjectsByType<EnemySpawnIdentity>().Length == alive && session.CurrentExperience == experience && session.CurrentGold == gold && Time.timeScale == 1,
                "Beyond both spawn intervals: no respawn, damage rewards or currency changes behind GAME OVER");
            yield return Capture("GameOver");
            yield return Hover("Retry"); yield return Hover("BackToMainMenu"); yield return Capture("GameOver_HoverBack");
            yield return Hover("Retry");
            yield return KeyPress(Key.DownArrow);
            Check(EventSystem.current.currentSelectedGameObject.name == "BackToMainMenu", "Keyboard navigates Game Over");
            yield return KeyPress(Key.UpArrow); yield return KeyPress(Key.Enter); yield return Loaded("Village"); Fresh();
            Boost();
            player = Player(); var shopMenu = Object.FindAnyObjectByType<ShopMenu>();
            var station = Object.FindAnyObjectByType<ShopStation>();
            var collider = station.GetComponent<Collider2D>();
            Move(player, new Vector2(collider.bounds.center.x, collider.bounds.min.y - player.GetComponent<Collider2D>().bounds.extents.y - .03f));
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", pad);
            yield return PadPress(GamepadButton.South);
            Check(shopMenu.IsOpen && !player.enabled && !player.GetComponent<PlayerAttack>().enabled, "Existing A Interact opens Shop and locks gameplay");
            player.GetComponent<PlayerHealth>().TakeDamage(int.MaxValue); yield return null; yield return null;
            Check(!shopMenu.IsOpen && Object.FindAnyObjectByType<GameOverUI>().IsVisible && EventSystem.current.currentSelectedGameObject.name == "Retry",
                "Death closes an open Shop and transfers shared UI focus to RETRY");
            yield return PadPress(GamepadButton.DpadDown); yield return ReturnWithoutLoadingScreen(); Unique(0);
            Check(GameFlow.State == RunState.MainMenu && !GameFlow.IsGameplayActive && Time.timeScale == 1 && GameSession.Instance.CurrentGold == 0 &&
                  GameSession.Instance.CurrentExperience == 0 && GameSession.Instance.ShopMaxHealthBonus == 0 && GameSession.Instance.PlayerHealth == 0,
                "BACK TO MAIN MENU ends session, clears Game Over/progression and leaves timeScale 1");
            yield return Click("Play"); yield return Loaded("Village"); Fresh();
            // E still works after returning to the main menu and starting again.
            player = Player(); var well = Object.FindAnyObjectByType<LevelingStation>();
            collider = (Collider2D)new SerializedObject(well).FindProperty("blockingCollider").objectReferenceValue;
            Move(player, new Vector2(collider.bounds.center.x, collider.bounds.min.y - player.GetComponent<Collider2D>().bounds.extents.y - .03f));
            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            yield return KeyPress(Key.E);
            Check(Object.FindAnyObjectByType<LevelingMenu>().IsOpen, "E Interact opens existing leveling UI after menu-to-new-run cycle");
            player.GetComponent<PlayerHealth>().TakeDamage(int.MaxValue); yield return null; yield return null;
            Check(!Object.FindAnyObjectByType<LevelingMenu>().IsOpen && Object.FindAnyObjectByType<GameOverUI>().IsVisible, "Death also closes leveling UI without restoring old gameplay locks");
            yield return Click("Retry"); yield return Loaded("Village"); Fresh();
            yield return new WaitForSecondsRealtime(.1f); // Existing two-frame portal re-entry guard.
            portal = Object.FindObjectsByType<ScenePortal>().Single(p => p.DestinationScene == "Plateau");
            Check(portal.TransitionService.TryTransition("Plateau", portal.DestinationSpawnPointId, Player()), "Retried run can enter Plateau normally");
            yield return Loaded("Plateau"); spawner = Object.FindAnyObjectByType<EnemySpawner>(); spawner.Spawned += Freeze;
            Check(spawner.enabled && spawner.CountAlive("Goblin") == 0 && spawner.CountAlive("Cyclops") == 0, "Plateau re-entry begins with fresh enabled spawner and zero old enemy counts");
            yield return new WaitForSecondsRealtime(3.2f);
            Check(spawner.CountAlive("Goblin") == 1 && spawner.CountAlive("Cyclops") == 1 && Object.FindObjectsByType<EnemyProgression>().All(e => e.Level == 1),
                "Fresh Plateau clocks spawn exactly one of each at reset level 1; no catch-up burst");
            PadState(Vector2.zero, GamepadButton.South); yield return null;
            Player().GetComponent<PlayerHealth>().TakeDamage(int.MaxValue); yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.2f);
            Check(GameFlow.State == RunState.GameOver && !SceneTransitionService.IsTransitioning, "Held Submit on death never triggers an accidental RETRY");
            PadState(Vector2.zero); yield return new WaitForSecondsRealtime(.15f);
            yield return PadPress(GamepadButton.South); yield return Loaded("Village"); Fresh();
            Check(Time.frameCount > report.firstFrame + 100, "Play-mode verification advanced over 100 actual frames");
        }
    }
}
