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
    /// <summary>Explicit integration test against the actual saved scenes, with temporary synthetic devices.</summary>
    [InitializeOnLoad]
    public static class M13PauseValidation
    {
        [Serializable] private sealed class Report
        {
            public string status = "running";
            public List<string> passed = new();
            public List<string> errors = new();
            public int firstFrame, lastFrame;
            public bool domainReloadDisabled;
        }
        public const string ResultPath = "Library/M13PauseValidation.json";
        private const string Pending = "LEVELS.M13.Pending";
        private const string RestoreBackground = "LEVELS.M13.RestoreBackground";
        private static Report report;
        private static Gamepad pad;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static Vector2 stick;
        private static readonly FieldInfo Clock = typeof(EnemySpawner).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
        static M13PauseValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        private static void Check(bool value, string description)
        {
            if (!value) throw new InvalidOperationException(description);
            report.passed.Add(description);
        }
        private static T Ref<T>(Object component, string field) where T : Object =>
            (T)new SerializedObject(component).FindProperty(field).objectReferenceValue;

        [MenuItem("LEVEL's/M13/Validate Saved Pause Assets")]
        public static void ValidateSavedAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            report = new Report();
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
                var action = asset.FindAction("System/Pause", true);
                Check(action.type == InputActionType.Button && string.IsNullOrEmpty(action.interactions) &&
                      action.bindings.Count == 2 && action.bindings.Any(b => b.path == "<Keyboard>/escape") &&
                      action.bindings.Any(b => b.path == "<Gamepad>/start") && action.bindings.All(b => string.IsNullOrEmpty(b.interactions)),
                    "Existing input asset: System/Pause is a press-only Button with Escape and Gamepad/start, no Hold");
                Check(asset.FindAction("Player/Attack").bindings.Any(b => b.path == "<Gamepad>/rightShoulder") &&
                      asset.FindAction("Player/Interact").bindings.Any(b => b.path == "<Gamepad>/buttonSouth") &&
                      asset.FindAction("Player/Interact").bindings.Any(b => b.path == "<Keyboard>/e"), "RB/R1 and E/A Interact bindings preserved");
                foreach (string name in new[] { "MainMenu", "Village", "Plateau", "Tower" })
                {
                    var scene = EditorSceneManager.OpenScene(M12MenuSetup.Root + "Scenes/" + name + ".unity");
                    var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                    Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), name + ": no missing scripts");
                    foreach (var c in all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c != null))
                    {
                        var iterator = new SerializedObject(c).GetIterator();
                        while (iterator.NextVisible(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null &&
                                !iterator.objectReferenceEntityIdValue.Equals(default(EntityId)))
                                throw new InvalidOperationException(name + ": broken reference " + c.name + "." + iterator.propertyPath);
                    }
                    Check(Object.FindObjectsByType<EventSystem>().Length == 1, name + ": exactly one shared EventSystem");
                    var controllers = Object.FindObjectsByType<PauseController>();
                    var menus = Object.FindObjectsByType<PauseMenuUI>(FindObjectsInactive.Include);
                    Check(controllers.Length == (name == "MainMenu" ? 0 : 1) && menus.Length == controllers.Length, name + ": correct scene-owned pause component count");
                    if (name == "MainMenu") continue;
                    var controller = controllers.Single(); var ui = menus.Single();
                    Check(Ref<GameFlow>(controller, "flow") != null && Ref<PauseMenuUI>(controller, "menu") == ui &&
                          Ref<InputActionReference>(controller, "pauseAction").action.id == action.id && Ref<PauseController>(ui, "controller") == controller,
                        name + ": pause action, flow and UI references assigned");
                    Check(!Ref<GameObject>(ui, "panel").activeSelf && ui.GetComponent<Canvas>().sortingOrder == 150 &&
                          ui.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, name + ": saved inactive overlay below Game Over/loading with raycaster");
                    Check(ui.GetComponentsInChildren<TextMeshProUGUI>(true).All(t => t.text.All(c => t.font.HasCharacter(c))), name + ": pixel font covers all pause labels");
                    var focus = ui.GetComponentInChildren<MenuFocus>(true);
                    Check(Ref<InputSystemUIInputModule>(focus, "inputModule") != null && Ref<UnityEngine.UI.Button>(focus, "firstButton").name == "Continue" &&
                          Ref<UnityEngine.UI.Button>(ui, "continueButton").navigation.selectOnDown == Ref<UnityEngine.UI.Button>(ui, "mainMenuButton"),
                        name + ": shared UI module, initial Continue focus and explicit button navigation");
                    foreach (var spawner in Object.FindObjectsByType<EnemySpawner>())
                        Check(spawner.Enemies.Count == 2 && spawner.Enemies.Single(e => e.category == "Goblin").maxAlive == 5 &&
                              spawner.Enemies.Single(e => e.category == "Cyclops").maxAlive == 2, name + ": independent limits remain 5 Goblins and 2 Cyclops");
                }
                report.status = "passed";
            }
            catch (Exception e) { report.status = "failed"; report.errors.Add(e.ToString()); throw; }
            finally
            {
                File.WriteAllText("Library/M13SavedAssetsValidation.json", JsonUtility.ToJson(report, true));
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("LEVEL's/M13/Run Pause Play Mode Validation")]
        public static void Start()
        {
            ValidateSavedAssets();
            SessionState.SetBool(RestoreBackground, Application.runInBackground);
            SessionState.SetBool(Pending, true);
            Application.runInBackground = true;
            EditorSceneManager.OpenScene(M12MenuSetup.MenuPath);
            LevelsPlayModeSetup.UseValidationScene(M12MenuSetup.MenuPath);
            EditorApplication.isPlaying = true;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("LEVELS.M13.Restore", false))
            {
                Application.runInBackground = SessionState.GetBool(RestoreBackground, false);
                SessionState.EraseBool("LEVELS.M13.Restore");
                var completed = JsonUtility.FromJson<Report>(File.ReadAllText(ResultPath));
                if (Time.timeScale == 1f) completed.passed.Add("Stopping Play mode while paused restores timeScale to 1 through production cleanup");
                else { completed.errors.Add("Play-mode teardown retained a paused timeScale"); completed.status = "failed"; }
                File.WriteAllText(ResultPath, JsonUtility.ToJson(completed, true));
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); SessionState.SetBool("LEVELS.M13.Restore", true);
            report = new Report { firstFrame = Time.frameCount, domainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0 };
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
            report.lastFrame = Time.frameCount; report.status = report.errors.Count == 0 ? "passed" : "failed";
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            Debug.Log("M13 validation: " + report.status + ", " + report.passed.Count + " checks. " + ResultPath);
            EditorApplication.isPlaying = false;
        }

        private static PlayerMovement Player() => Object.FindAnyObjectByType<PlayerMovement>();
        private static PauseController Controller() => Object.FindAnyObjectByType<PauseController>();
        private static PauseMenuUI UI() => Object.FindAnyObjectByType<PauseMenuUI>();
        private static void Pad(params GamepadButton[] buttons)
        {
            var state = new GamepadState { leftStick = stick };
            foreach (var b in buttons) state = state.WithButton(b);
            InputSystem.QueueStateEvent(pad, state);
        }
        private static IEnumerator PadPress(GamepadButton button)
        {
            Pad(button); yield return new WaitForSecondsRealtime(.06f);
            Pad(); yield return new WaitForSecondsRealtime(.10f);
        }
        private static IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return new WaitForSecondsRealtime(.06f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.10f);
        }
        private static IEnumerator Loaded(string name)
        {
            float end = Time.realtimeSinceStartup + 15;
            while ((SceneTransitionService.IsTransitioning || SceneManager.GetActiveScene().name != name) && Time.realtimeSinceStartup < end) yield return null;
            yield return null; yield return null;
            Check(SceneManager.GetActiveScene().name == name && !SceneTransitionService.IsTransitioning, "Async loading completed: " + name);
        }
        private static string SessionData()
        {
            var s = GameSession.Instance;
            return $"{s.CurrentExperience}/{s.TotalExperience}/{s.CurrentGold}/{s.PlayerHealth}/{s.PlayerMaxHealth}/{s.PlayerLevel}/{s.GoblinLevel}/{s.CyclopsLevel}/{s.ShopMaxHealthBonus}";
        }
        private static void Paused(string message)
        {
            Check(GameFlow.State == RunState.Paused && !GameFlow.IsGameplayActive && Time.timeScale == 0 && UI().IsVisible &&
                  !Player().enabled && !Player().GetComponent<PlayerAttack>().enabled && Player().GetComponent<PlayerInput>().enabled,
                message);
        }
        private static void Resumed(string message)
        {
            Check(GameFlow.IsGameplayActive && Time.timeScale == 1 && !UI().IsVisible && Player().enabled && Player().GetComponent<PlayerAttack>().enabled, message);
        }
        private static IEnumerator MouseClick(string name)
        {
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b => b.name == name);
            Canvas.ForceUpdateCanvases(); Vector2 position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.15f);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Check(hits.Any(h => h.gameObject == button.gameObject), name + ": real mouse raycast reaches button");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.06f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.10f);
        }
        private static void FreezeNewEnemy(EnemySpawnIdentity enemy)
        {
            enemy.GetComponent<EnemyAI>().enabled = false;
            enemy.GetComponent<ContactDamage>().enabled = false;
        }

        private static IEnumerator Run()
        {
            yield return null; yield return null;
            stick = Vector2.zero;
            pad = InputSystem.AddDevice<Gamepad>("M13_TestPad"); keyboard = InputSystem.AddDevice<Keyboard>("M13_TestKeyboard"); mouse = InputSystem.AddDevice<Mouse>("M13_TestMouse");
            yield return new WaitForSecondsRealtime(.2f);
            yield return KeyPress(Key.Escape); yield return PadPress(GamepadButton.Start);
            Check(GameFlow.State == RunState.MainMenu && Time.timeScale == 1 && Controller() == null, "Escape/Start cannot pause Main Menu; no stale controller or timeScale");
            Check(GameFlow.Instance.StartNewRun(), "Start fresh run through existing GameFlow");
            yield return KeyPress(Key.Escape); yield return PadPress(GamepadButton.Start);
            Check(GameFlow.State == RunState.Loading && Time.timeScale == 1, "Escape/Start cannot layer Pause over loading");
            yield return Loaded("Village");
            var player = Player(); var session = GameSession.Instance;
            session.AddExperience(123); session.AddGold(45); string data = SessionData();
            yield return KeyPress(Key.Escape); Paused("60ms Escape press opens Pause and synchronously locks movement/combat without disabling PlayerInput");
            Check(!Object.FindAnyObjectByType<ShopMenu>().Open(player, Object.FindAnyObjectByType<ShopTransactions>()) &&
                  !Object.FindAnyObjectByType<LevelingMenu>().Open(player), "Shop and Leveling cannot open over Pause");
            Check(EventSystem.current.currentSelectedGameObject.name == "Continue" && Object.FindAnyObjectByType<InputSystemUIInputModule>().enabled,
                "Pause UI stays active at timeScale zero with Continue selected");
            yield return KeyPress(Key.DownArrow);
            Check(EventSystem.current.currentSelectedGameObject.name == "BackToMainMenu", "Keyboard navigates paused UI using unscaled time");
            yield return KeyPress(Key.UpArrow); yield return KeyPress(Key.Enter);
            Resumed("Keyboard Continue closes overlay and restores timeScale, movement and combat");
            Check(GameSession.Instance == session && SessionData() == data, "Pause/Continue preserves every GameSession value and the instance");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return new WaitForSecondsRealtime(.45f);
            Paused("Held Escape produces only one toggle");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.1f);
            yield return KeyPress(Key.Escape); Resumed("A second Escape press resumes without reopening on release");

            var shop = Object.FindAnyObjectByType<ShopMenu>(); var leveling = Object.FindAnyObjectByType<LevelingMenu>();
            Check(shop.Open(player, Object.FindAnyObjectByType<ShopTransactions>()), "Open existing Shop");
            Check(!GameFlow.Instance.TryPause(), "GameFlow rejects Pause while Shop is open");
            yield return KeyPress(Key.Escape);
            Check(!shop.IsOpen && GameFlow.IsGameplayActive && !UI().IsVisible, "Escape closes only Shop, without layering Pause");
            Check(shop.Open(player, Object.FindAnyObjectByType<ShopTransactions>()), "Reopen Shop for controller test");
            yield return PadPress(GamepadButton.Start);
            Check(!shop.IsOpen && GameFlow.IsGameplayActive && !UI().IsVisible, "Start closes only Shop");
            Check(leveling.Open(player), "Open existing Leveling menu"); yield return KeyPress(Key.Escape);
            Check(!leveling.IsOpen && GameFlow.IsGameplayActive && !UI().IsVisible, "Escape closes only Leveling");
            Check(leveling.Open(player), "Reopen Leveling for controller test"); yield return PadPress(GamepadButton.Start);
            Check(!leveling.IsOpen && GameFlow.IsGameplayActive && !UI().IsVisible, "Start closes only Leveling");

            player.GetComponent<PlayerInput>().SwitchCurrentControlScheme("Gamepad", pad);
            stick = Vector2.right; Pad(); yield return new WaitForSecondsRealtime(.15f);
            yield return PadPress(GamepadButton.Start); Paused("60ms Gamepad/start press opens Pause while stick remains held");
            Vector3 stopped = player.transform.position; float scaled = Time.time; int frame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.25f);
            Check(player.transform.position == stopped && player.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero && Time.time == scaled && Time.frameCount > frame,
                "Gameplay clock and Player position stop while actual UI frames continue");
            ScreenCapture.CaptureScreenshot("Library/M13_Paused.png"); yield return null; yield return null;
            yield return PadPress(GamepadButton.Start); Resumed("Second Start resumes exactly once");
            Vector3 before = player.transform.position; yield return new WaitForSecondsRealtime(.15f);
            Check(player.GetComponent<PlayerInputReader>().MoveInput.x > .8f && player.transform.position.x > before.x + .1f,
                "Continuously held analog stick moves after Resume without release or new input edge");
            stick = Vector2.zero; Pad(); yield return new WaitForSecondsRealtime(.1f);
            Pad(GamepadButton.Start); yield return new WaitForSecondsRealtime(.4f); Paused("Held Start produces only one toggle");
            Pad(); yield return new WaitForSecondsRealtime(.1f);
            yield return PadPress(GamepadButton.DpadDown);
            Check(EventSystem.current.currentSelectedGameObject.name == "BackToMainMenu", "Gamepad navigates paused UI");
            yield return PadPress(GamepadButton.DpadUp); yield return PadPress(GamepadButton.South); Resumed("Gamepad A activates Continue");
            // Two devices in one input update still represent a single pause request.
            Pad(GamepadButton.Start); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return new WaitForSecondsRealtime(.15f); Paused("Simultaneous Escape and Start produce one toggle");
            Pad(); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.1f);
            yield return MouseClick("Continue"); Resumed("Mouse click activates Continue at timeScale zero");

            var portal = Object.FindObjectsByType<ScenePortal>().Single(p => p.DestinationScene == "Plateau");
            Check(portal.TransitionService.TryTransition("Plateau", portal.DestinationSpawnPointId, player), "Existing portal starts Plateau transition");
            yield return KeyPress(Key.Escape); yield return PadPress(GamepadButton.Start);
            Check(SceneTransitionService.IsTransitioning && Time.timeScale == 1 && !UI().IsVisible, "Pause blocked during portal loading");
            yield return Loaded("Plateau"); player = Player();
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            yield return new WaitForSecondsRealtime(3.2f);
            Check(spawner.CountAlive("Goblin") >= 1 && spawner.CountAlive("Cyclops") >= 1, "Real saved spawner creates both categories before pausing");
            var enemy = Object.FindObjectsByType<EnemyAI>().First();
            // Put one live enemy into detection range to prove actual AI movement stops and resumes.
            var enemyBody = enemy.GetComponent<Rigidbody2D>(); var playerBody = player.GetComponent<Rigidbody2D>();
            enemyBody.position = playerBody.position + Vector2.up * 2f; enemy.transform.position = enemyBody.position; Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(enemyBody.linearVelocity.sqrMagnitude > .01f, "Live enemy AI moves before pause");
            yield return KeyPress(Key.Escape); Paused("Pause opens in combat scene");
            var clocks = (float[])Clock.GetValue(spawner); var frozenClocks = (float[])clocks.Clone();
            int goblins = spawner.CountAlive("Goblin"), cyclops = spawner.CountAlive("Cyclops"), attempts = spawner.LastAttemptCount;
            Vector3 enemyPosition = enemy.transform.position; scaled = Time.time; data = SessionData();
            Check(!player.GetComponent<PlayerAttack>().TryAttack() && !player.GetComponent<PlayerHealth>().TakeDamage(1) &&
                  !enemy.GetComponent<EnemyHealth>().TakeDamage(1), "Pause rejects attacks, Player damage and enemy damage through real gameplay APIs");
            Check(Object.FindObjectsByType<ShopMenu>().All(m => !m.IsOpen) && Object.FindObjectsByType<LevelingMenu>().All(m => !m.IsOpen),
                "Combat scene has no station menu layered over Pause");
            yield return new WaitForSecondsRealtime(3.4f);
            Check(spawner.enabled && ReferenceEquals(clocks, Clock.GetValue(spawner)) && clocks.SequenceEqual(frozenClocks) && spawner.LastAttemptCount == attempts &&
                  spawner.CountAlive("Goblin") == goblins && spawner.CountAlive("Cyclops") == cyclops,
                "Beyond both spawn intervals: same independent timer array/values, limits and counts; no spawn attempts");
            Check(enemy.transform.position == enemyPosition && enemyBody.linearVelocity == Vector2.zero && enemy.enabled && Time.time == scaled && SessionData() == data,
                "Live EnemyAI, scaled clock and session remain unchanged throughout pause");
            Controller().Continue(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Resumed("Combat-scene Continue restores gameplay");
            Check(enemyBody.linearVelocity.sqrMagnitude > .01f && ReferenceEquals(clocks, Clock.GetValue(spawner)) && clocks[0] >= frozenClocks[0] && clocks[1] >= frozenClocks[1],
                "Same AI and both existing spawn clocks continue after Resume");
            foreach (var existing in Object.FindObjectsByType<EnemySpawnIdentity>()) FreezeNewEnemy(existing);
            spawner.Spawned += FreezeNewEnemy;
            // Seed distinct remaining durations through reflection, without changing production intervals.
            clocks[0] = spawner.Enemies[0].spawnInterval - .3f; clocks[1] = .05f;
            yield return KeyPress(Key.Escape); Paused("Pause preserves deliberately different category clock phases");
            frozenClocks = (float[])clocks.Clone(); goblins = spawner.CountAlive("Goblin"); cyclops = spawner.CountAlive("Cyclops");
            yield return new WaitForSecondsRealtime(3.2f);
            Check(clocks.SequenceEqual(frozenClocks), "Distinct Goblin/Cyclops timer remainders stay frozen");
            Controller().Continue(); yield return new WaitForSecondsRealtime(.12f);
            Check(spawner.CountAlive("Goblin") == goblins && spawner.CountAlive("Cyclops") == cyclops, "No accumulated burst immediately after Resume");
            yield return new WaitForSecondsRealtime(.35f);
            Check(spawner.CountAlive("Goblin") == goblins + 1 && spawner.CountAlive("Cyclops") == cyclops, "Goblin resumes from remaining interval; Cyclops clock remains independent");
            yield return new WaitForSecondsRealtime(7f);
            Check(spawner.CountAlive("Goblin") == 5 && spawner.CountAlive("Cyclops") == 2, "Actual spawning fills and respects 5 Goblin / 2 Cyclops limits");
            yield return KeyPress(Key.Escape); Paused("Pause opens at full spawn limits");
            Controller().Continue(); yield return new WaitForSecondsRealtime(3.2f);
            Check(spawner.CountAlive("Goblin") == 5 && spawner.CountAlive("Cyclops") == 2, "Resume cannot exceed either category limit");
            yield return KeyPress(Key.Escape);
            Check(!GameFlow.Instance.StartNewRun(), "Paused run cannot accidentally start/reset another run");
            var oldSpawner = spawner;
            yield return MouseClick("BackToMainMenu"); yield return Loaded("MainMenu");
            Check(oldSpawner == null && Controller() == null && Object.FindAnyObjectByType<EnemySpawner>() == null &&
                  GameFlow.State == RunState.MainMenu && Time.timeScale == 1,
                "Back to Main Menu unloads old spawner and pause state with timeScale 1");
            Check(GameSession.Instance == session && session.CurrentExperience == 0 && session.CurrentGold == 0 && session.PlayerHealth == 0,
                "Back to Main Menu ends and clears the current GameSession");
            Check(Object.FindAnyObjectByType<InputSystemUIInputModule>().enabled && EventSystem.current.currentSelectedGameObject != null &&
                  EventSystem.current.currentSelectedGameObject.GetComponentInParent<MainMenuUI>() != null,
                "Main Menu navigation is active; stationary pointer may select its hovered button");
            Check(GameFlow.Instance.StartNewRun(), "Can start a new run after paused menu return"); yield return Loaded("Village");
            yield return KeyPress(Key.Escape); Paused("New scene owns exactly one fresh pause handler");
            yield return KeyPress(Key.Escape); Resumed("Fresh handler resumes once without stale input subscriptions");
            Player().GetComponent<PlayerHealth>().TakeDamage(int.MaxValue); yield return null; yield return null;
            yield return KeyPress(Key.Escape); yield return PadPress(GamepadButton.Start);
            Check(GameFlow.State == RunState.GameOver && !UI().IsVisible && Object.FindAnyObjectByType<GameOverUI>().IsVisible && Time.timeScale == 1,
                "Escape/Start cannot layer Pause over Game Over");
            Check(Time.frameCount > report.firstFrame + 100, "Validation advanced more than 100 actual Play-mode frames");
            Check(GameFlow.Instance.StartNewRun(), "Retry remains available after Game Over pause rejection"); yield return Loaded("Village");
            yield return KeyPress(Key.Escape); Paused("Finish validation paused to exercise Play-mode teardown cleanup");
        }
    }
}
