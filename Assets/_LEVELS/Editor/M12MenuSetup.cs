using System;
using System.Linq;
using Levels.Core;
using Levels.Player;
using Levels.Scenes;
using Levels.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Explicit editor authoring only. Produces saved, editable scenes, UI and a local pixel font.</summary>
    public static class M12MenuSetup
    {
        public const string Root = "Assets/_LEVELS/";
        public const string MenuPath = Root + "Scenes/MainMenu.unity";
        public const string GameOverPath = Root + "Prefabs/UI/GameOverScreen.prefab";
        public const string FontPath = Root + "Art/Fonts/RetroMenu.asset";
        private static readonly Color Gold = new Color(.94f, .76f, .39f);

        [MenuItem("LEVEL's/M12/Configure Main Menu and Game Over")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var font = CreateFont();
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GameOverPath) == null) CreateGameOver(font);
                var gameOverRoot = PrefabUtility.LoadPrefabContents(GameOverPath);
                try
                {
                    gameOverRoot.GetComponent<Canvas>().sortingOrder = 200; // Above HUD (100), below LoadingScreen (1000).
                    PrefabUtility.SaveAsPrefabAsset(gameOverRoot, GameOverPath);
                }
                finally { Object.DestroyImmediate(gameOverRoot); }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) == null) CreateMainMenu(font);
                foreach (string name in new[] { "Village", "Plateau", "Tower" }) ConfigureGameplayScene(name);
                var scenes = EditorBuildSettings.scenes.Where(s => s.path != MenuPath).ToList();
                scenes.Insert(0, new EditorBuildSettingsScene(MenuPath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
                if (profile != null && profile.overrideGlobalScenes)
                {
                    var profileScenes = profile.scenes.Where(s => s.path != MenuPath).ToList();
                    profileScenes.Insert(0, new EditorBuildSettingsScene(MenuPath, true));
                    profile.scenes = profileScenes.ToArray();
                    EditorUtility.SetDirty(profile);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            LevelsPlayModeSetup.RestoreVillageStart();
            Debug.Log("M12: saved MainMenu, GameOverScreen and all three gameplay scenes configured; MainMenu is first build/start scene.");
        }

        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static GameObject UIObject(string name, Transform parent, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = LayerMask.NameToLayer("UI");
            obj.transform.SetParent(parent, false);
            obj.GetComponent<RectTransform>().sizeDelta = size;
            return obj;
        }

        private static GameObject CanvasRoot(string name)
        {
            var root = UIObject(name, null, new Vector2(1280, 720));
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return root;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string value, TMP_FontAsset font, float size, float y)
        {
            var obj = UIObject(name, parent, new Vector2(680, 110));
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, y);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size;
            text.text = value; text.alignment = TextAlignmentOptions.Center; text.color = Gold;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string label, TMP_FontAsset font, float y)
        {
            var obj = UIObject(name, parent, new Vector2(560, 68));
            obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, y);
            var image = obj.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white;
            var button = obj.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(.10f, .15f, .21f);
            colors.selectedColor = new Color(.31f, .38f, .42f);
            // MenuFocus owns hover selection, so hover alone must not light a second button.
            colors.highlightedColor = colors.normalColor;
            colors.pressedColor = new Color(.45f, .34f, .17f); colors.fadeDuration = .06f;
            button.colors = colors;
            var outline = obj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Gold; outline.effectDistance = new Vector2(2, -2);
            var text = Label("Label", obj.transform, label, font, 28, 0);
            text.rectTransform.sizeDelta = new Vector2(540, 62);
            return button;
        }

        private static GameObject Panel(GameObject root, TMP_FontAsset font, bool gameOver,
            out UnityEngine.UI.Button first, out UnityEngine.UI.Button second)
        {
            var panel = UIObject("Panel", root.transform, Vector2.zero);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .035f, .055f, gameOver ? .96f : 1f);
            var content = UIObject("Content", panel.transform, new Vector2(700, 500));
            var title = Label("Title", content.transform, gameOver ? "GAME OVER" : "LEVEL's", font, gameOver ? 64 : 84, 135);
            if (gameOver) title.color = new Color(.94f, .49f, .35f);
            var rule = UIObject("Rule", content.transform, new Vector2(120, 4));
            rule.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 52);
            var ruleImage = rule.AddComponent<UnityEngine.UI.Image>(); ruleImage.color = Gold; ruleImage.raycastTarget = false;
            first = Button(gameOver ? "Retry" : "Play", content.transform, gameOver ? "RETRY" : "PLAY", font, -25);
            second = Button(gameOver ? "BackToMainMenu" : "Exit", content.transform, gameOver ? "BACK TO MAIN MENU" : "EXIT", font, -117);
            first.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = second, selectOnDown = second };
            second.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = first, selectOnDown = first };
            var focus = panel.AddComponent<MenuFocus>(); Set(focus, "firstButton", first);
            return panel;
        }

        private static InputSystemUIInputModule EnsureEventSystem()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            if (systems.Length > 1) throw new InvalidOperationException("Scene has multiple EventSystems.");
            var events = systems.Length == 1 ? systems[0] : new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
            module.enabled = false; module.UnassignActions();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            var references = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions)).OfType<InputActionReference>()
                .Where(r => (r.hideFlags & HideFlags.HideInHierarchy) == 0).ToArray();
            InputActionReference Ref(string name) => references.First(r => r.action.id == actions.FindAction("UI/" + name, true).id);
            module.actionsAsset = actions;
            module.move = Ref("Navigate"); module.submit = Ref("Submit"); module.cancel = Ref("Cancel");
            module.point = Ref("Point"); module.leftClick = Ref("Click"); module.scrollWheel = Ref("ScrollWheel");
            module.deselectOnBackgroundClick = false;
            EditorUtility.SetDirty(module);
            if (PrefabUtility.IsPartOfPrefabInstance(module)) PrefabUtility.RecordPrefabInstancePropertyModifications(module);
            return module;
        }

        private static void CreateGameOver(TMP_FontAsset font)
        {
            var root = CanvasRoot("GameOverScreen");
            try
            {
                var panel = Panel(root, font, true, out var retry, out var back);
                var ui = root.AddComponent<GameOverUI>();
                Set(ui, "panel", panel); Set(ui, "retryButton", retry); Set(ui, "mainMenuButton", back);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, GameOverPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateMainMenu(TMP_FontAsset font)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .055f); camera.transform.position = new Vector3(0, 0, -10);
            var root = CanvasRoot("MainMenuUI");
            Panel(root, font, false, out var play, out var exit);
            var menu = root.AddComponent<MainMenuUI>(); Set(menu, "playButton", play); Set(menu, "exitButton", exit);
            var module = EnsureEventSystem(); Set(root.GetComponentInChildren<MenuFocus>(), "inputModule", module);
            var loading = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/UI/LoadingScreen.prefab"));
            loading.SetActive(false);
            var service = new GameObject("SceneTransitions").AddComponent<SceneTransitionService>(); Set(service, "loadingScreen", loading.GetComponent<LoadingScreen>());
            var flow = new GameObject("GameFlow").AddComponent<GameFlow>(); Set(flow, "transitions", service);
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        private static void ConfigureGameplayScene(string name)
        {
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity");
            var module = EnsureEventSystem();
            var ui = Object.FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
            if (ui == null) ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GameOverPath))).GetComponent<GameOverUI>();
            Set(ui.GetComponentInChildren<MenuFocus>(true), "inputModule", module);
            var flow = Object.FindAnyObjectByType<GameFlow>();
            if (flow == null) flow = new GameObject("GameFlow").AddComponent<GameFlow>();
            Set(flow, "transitions", Object.FindAnyObjectByType<SceneTransitionService>()); Set(flow, "gameOverUI", ui);
            if (name == "Village" && !Object.FindObjectsByType<SpawnPoint>().Any(p => p.Id == "NewRun"))
            {
                var point = new GameObject("Spawn_NewRun").AddComponent<SpawnPoint>();
                point.transform.SetParent(GameObject.Find("SpawnPoints").transform);
                point.transform.position = Object.FindAnyObjectByType<PlayerMovement>().transform.position;
                var so = new SerializedObject(point); so.FindProperty("id").stringValue = "NewRun"; so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static TMP_FontAsset CreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); if (existing != null) return existing;
            // Original 5x7 bitmap alphabet, stored as an embedded atlas. No external font dependency.
            string[] rows = {
                "01110/10001/10001/11111/10001/10001/10001", "11110/10001/10001/11110/10001/10001/11110",
                "01111/10000/10000/10000/10000/10000/01111", "11110/10001/10001/10001/10001/10001/11110",
                "11111/10000/10000/11110/10000/10000/11111", "11111/10000/10000/11110/10000/10000/10000",
                "01111/10000/10000/10111/10001/10001/01111", "10001/10001/10001/11111/10001/10001/10001",
                "11111/00100/00100/00100/00100/00100/11111", "00111/00010/00010/00010/10010/10010/01100",
                "10001/10010/10100/11000/10100/10010/10001", "10000/10000/10000/10000/10000/10000/11111",
                "10001/11011/10101/10101/10001/10001/10001", "10001/11001/10101/10011/10001/10001/10001",
                "01110/10001/10001/10001/10001/10001/01110", "11110/10001/10001/11110/10000/10000/10000",
                "01110/10001/10001/10001/10101/10010/01101", "11110/10001/10001/11110/10100/10010/10001",
                "01111/10000/10000/01110/00001/00001/11110", "11111/00100/00100/00100/00100/00100/00100",
                "10001/10001/10001/10001/10001/10001/01110", "10001/10001/10001/10001/10001/01010/00100",
                "10001/10001/10001/10101/10101/11011/10001", "10001/10001/01010/00100/01010/10001/10001",
                "10001/10001/01010/00100/00100/00100/00100", "11111/00001/00010/00100/01000/10000/11111",
                "00000/00000/01111/10000/01110/00001/11110", "00100/00100/00000/00000/00000/00000/00000",
                "00000/00000/00000/00000/00000/00000/00000"
            };
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZs' ";
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>(); font.name = "RetroMenu";
            font.faceInfo = new FaceInfo { familyName = "LEVELS Retro Menu", styleName = "Regular", pointSize = 28, scale = 1,
                lineHeight = 36, ascentLine = 28, capLine = 28, meanLine = 20, baseline = 0, descentLine = -4 };
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            var texture = new Texture2D(256, 128, TextureFormat.Alpha8, false) { name = "RetroMenu Atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[256 * 128];
            for (int i = 0; i < chars.Length; i++)
            {
                int x = i % 10 * 24 + 2, y = i / 10 * 36 + 2;
                var pattern = rows[i].Split('/');
                for (int py = 0; py < 28; py++) for (int px = 0; px < 20; px++)
                    pixels[(y + py) * 256 + x + px] = new Color(1, 1, 1, pattern[6 - py / 4][px / 4] == '1' ? 1 : 0);
                var glyph = new Glyph((uint)i + 1, new GlyphMetrics(20, 28, 0, 28, chars[i] == ' ' ? 14 : 24), new GlyphRect(x, y, 20, 28), 1, 0);
                font.glyphTable.Add(glyph); font.characterTable.Add(new TMP_Character(chars[i], font, glyph));
            }
            texture.SetPixels(pixels); texture.Apply(); font.atlasTextures = new[] { texture };
            var material = new Material(Shader.Find("TextMeshPro/Bitmap")) { name = "RetroMenu Bitmap" };
            material.mainTexture = texture; font.material = material;
            var so = new SerializedObject(font); so.FindProperty("m_Version").stringValue = "1.1.0";
            so.FindProperty("m_AtlasWidth").intValue = 256; so.FindProperty("m_AtlasHeight").intValue = 128;
            so.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.RASTER; so.ApplyModifiedPropertiesWithoutUndo();
            font.ReadFontAssetDefinition();
            AssetDatabase.CreateAsset(font, FontPath); AssetDatabase.AddObjectToAsset(texture, font); AssetDatabase.AddObjectToAsset(material, font);
            EditorUtility.SetDirty(font);
            return font;
        }
    }
}
