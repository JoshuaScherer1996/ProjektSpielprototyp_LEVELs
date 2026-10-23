using System;
using System.Linq;
using Levels.Core;
using Levels.Enemies;
using Levels.Player;
using Levels.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Levels.Editor
{
    public static class M5ProgressionSetup
    {
        private const string Root = "Assets/_LEVELS/";
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("LEVEL's/Setup M5 Progression")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before setup.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save existing scene changes before setup.");

            if (!AssetDatabase.IsValidFolder(Root + "Resources"))
                AssetDatabase.CreateFolder("Assets/_LEVELS", "Resources");
            if (AssetDatabase.LoadAssetAtPath<ProgressionSettings>(Root + "Resources/LevelsProgression.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ProgressionSettings>(),
                    Root + "Resources/LevelsProgression.asset");

            EditPrefab("Characters/Player", root => Ensure<PlayerProgression>(root));
            EditPrefab("Characters/Enemy_Goblin", root => Ensure<EnemyProgression>(root));
            EditPrefab("UI/PlayerHUD", root =>
            {
                Transform healthPanel = root.transform.Find("HealthPanel");
                UnityEngine.UI.Text gold = root.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                    .First(t => t.name == "GoldLabel");
                UnityEngine.UI.Text levels = root.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                    .FirstOrDefault(t => t.name == "LevelLabel");
                if (levels == null)
                {
                    GameObject clone = UnityEngine.Object.Instantiate(gold.gameObject, gold.transform.parent);
                    clone.name = "LevelLabel";
                    levels = clone.GetComponent<UnityEngine.UI.Text>();
                }
                levels.text = "Player Lv 1  |  Enemies Lv 1";
                levels.fontSize = 20;
                levels.rectTransform.anchoredPosition = gold.rectTransform.anchoredPosition + new Vector2(0, -30);
                healthPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 146);
                Set(root.GetComponent<PlayerHealthHUD>(), "levelText", levels);
            });

            string menuPath = Root + "Prefabs/UI/LevelingMenu.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(menuPath) == null) CreateMenu(menuPath);
            EditPrefab("UI/LevelingMenu", root => ConfigureInput(root.GetComponentInChildren<InputSystemUIInputModule>(true)));
            string stationPath = Root + "Prefabs/Environment/LevelingStation.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(stationPath) == null) CreateStation(stationPath);
            EditPrefab("Environment/LevelingStation", ConfigureStationLabels);

            var scene = EditorSceneManager.OpenScene(Root + "Scenes/Village.unity");
            LevelingMenu menu = UnityEngine.Object.FindAnyObjectByType<LevelingMenu>(FindObjectsInactive.Include);
            if (menu == null)
                menu = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(menuPath)))
                    .GetComponent<LevelingMenu>();
            LevelingStation station = UnityEngine.Object.FindAnyObjectByType<LevelingStation>(FindObjectsInactive.Include);
            if (station == null)
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(stationPath));
                obj.transform.SetParent(GameObject.Find("Environment").transform);
                obj.transform.position = new Vector3(3, 0, 0);
                station = obj.GetComponent<LevelingStation>();
            }
            Set(station, "menu", menu);
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void EditPrefab(string name, Action<GameObject> edit)
        {
            string path = Root + "Prefabs/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T Ensure<T>(GameObject target) where T : Component => target.GetComponent<T>() ?? target.AddComponent<T>();
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject UIObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            if (parent != null) obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Rect(GameObject obj, Vector2 size, Vector2 position)
        {
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, string text, int fontSize)
        {
            var label = UIObject(name, parent).AddComponent<UnityEngine.UI.Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.text = text;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, float y)
        {
            GameObject obj = UIObject(name, parent);
            Rect(obj, new Vector2(420, 48), new Vector2(0, y));
            var image = obj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.16f, .3f, .4f, 1);
            var button = obj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.selectedColor = new Color(.35f, .85f, 1);
            colors.highlightedColor = colors.selectedColor;
            button.colors = colors;
            var label = Label("Label", obj.transform, text, 24);
            Rect(label.gameObject, new Vector2(410, 46), Vector2.zero);
            return button;
        }

        private static void CreateMenu(string path)
        {
            GameObject root = UIObject("LevelingMenu", null);
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
                root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                var controller = root.AddComponent<LevelingMenu>();
                var eventObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObj.transform.SetParent(root.transform);
                var module = eventObj.GetComponent<InputSystemUIInputModule>();
                ConfigureInput(module);

                var panel = UIObject("Panel", root.transform);
                var panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
                panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .82f);
                var card = UIObject("Card", panel.transform);
                Rect(card, new Vector2(600, 540), Vector2.zero);
                card.AddComponent<UnityEngine.UI.Image>().color = new Color(.06f, .09f, .13f, 1);
                var title = Label("Title", card.transform, "LEVELING STATION", 30);
                Rect(title.gameObject, new Vector2(560, 44), new Vector2(0, 218));
                var details = Label("Details", card.transform, "Player Level: 1     Enemy Level: 1", 22);
                Rect(details.gameObject, new Vector2(560, 160), new Vector2(0, 107));
                var player = Button("PlayerUpgrade", card.transform, "Level Up Player", -12);
                var enemies = Button("EnemyUpgrade", card.transform, "Level Up Enemies", -72);
                var close = Button("Close", card.transform, "Close", -132);
                var feedback = Label("Feedback", card.transform, "Choose one upgrade.", 20);
                Rect(feedback.gameObject, new Vector2(560, 60), new Vector2(0, -210));
                var buttons = new[] { player, enemies, close };
                for (int i = 0; i < buttons.Length; i++)
                {
                    var nav = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                        selectOnUp = buttons[(i + 2) % 3], selectOnDown = buttons[(i + 1) % 3] };
                    buttons[i].navigation = nav;
                }
                Set(controller, "panel", panel); Set(controller, "details", details); Set(controller, "feedback", feedback);
                Set(controller, "playerButton", player); Set(controller, "enemyButton", enemies);
                Set(controller, "closeButton", close); Set(controller, "inputModule", module);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ConfigureInput(InputSystemUIInputModule module)
        {
            module.enabled = false;
            // Clear auto-created transient references before assigning persistent importer subassets.
            // The module otherwise keeps an old reference when it points at the same action.
            module.UnassignActions();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            module.actionsAsset = actions;
            var references = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions))
                .OfType<InputActionReference>().Where(r => (r.hideFlags & HideFlags.HideInHierarchy) == 0).ToArray();
            module.move = references.First(r => r.action.id == actions.FindAction("UI/Navigate", true).id);
            module.submit = references.First(r => r.action.id == actions.FindAction("UI/Submit", true).id);
            module.cancel = references.First(r => r.action.id == actions.FindAction("UI/Cancel", true).id);
            module.point = references.First(r => r.action.id == actions.FindAction("UI/Point", true).id);
            module.leftClick = references.First(r => r.action.id == actions.FindAction("UI/Click", true).id);
            module.scrollWheel = references.First(r => r.action.id == actions.FindAction("UI/ScrollWheel", true).id);
            EditorUtility.SetDirty(module);
        }

        private static void CreateStation(string path)
        {
            var root = new GameObject("LevelingStation");
            try
            {
                var sprite = root.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Sprites/Square.png");
                sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Settings/GreyboxUnlit.mat");
                sprite.color = new Color(.9f, .65f, .15f);
                sprite.sortingLayerName = "Props";
                root.transform.localScale = new Vector3(.9f, 1.1f, 1);
                root.AddComponent<BoxCollider2D>();
                root.layer = LayerMask.NameToLayer("World");
                var station = root.AddComponent<LevelingStation>();
                ConfigureStationLabels(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ConfigureStationLabels(GameObject root)
        {
            if (root.transform.Find("StationLabels") != null) return;
            // Upgrade only the M5 station's own labels, preserving the station and collider.
            foreach (string name in new[] { "Sign", "InteractPrompt" })
            {
                Transform previous = root.transform.Find(name);
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }
            GameObject labels = UIObject("StationLabels", root.transform);
            labels.transform.localPosition = new Vector3(0, 1.1f, -.1f);
            labels.transform.localScale = Vector3.one * .01f;
            var canvas = labels.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Foreground";
            canvas.sortingOrder = 5;
            labels.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 100);
            var title = Label("Sign", labels.transform, "LEVEL UP", 28);
            Rect(title.gameObject, new Vector2(300, 42), Vector2.zero);
            var prompt = Label("InteractPrompt", labels.transform, "Hold E / Y / Triangle", 22);
            Rect(prompt.gameObject, new Vector2(320, 40), new Vector2(0, 43));
            prompt.gameObject.SetActive(false);
            Set(root.GetComponent<LevelingStation>(), "interactionPrompt", prompt.gameObject);
        }
    }
}
