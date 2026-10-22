using System;
using System.Linq;
using Levels.Core;
using Levels.Player;
using Levels.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Levels.Editor
{
    public static class M6ShopSetup
    {
        private const string Root = "Assets/_LEVELS/";

        [MenuItem("LEVEL's/Setup M6 Shop")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before setup.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save existing scene changes before setup.");

            string menuPath = Root + "Prefabs/UI/ShopMenu.prefab";
            string stationPath = Root + "Prefabs/Environment/ShopStation.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(menuPath) == null) CreateMenu(menuPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(stationPath) == null) CreateStation(stationPath);

            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(Root + "Scenes/Village.unity");
                var leveling = UnityEngine.Object.FindAnyObjectByType<LevelingMenu>(FindObjectsInactive.Include);
                if (leveling == null) throw new InvalidOperationException("Village requires its existing LevelingMenu.");
                var module = leveling.GetComponentInChildren<InputSystemUIInputModule>(true);
                if (module == null || UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length != 1)
                    throw new InvalidOperationException("Village must contain exactly one existing EventSystem.");

                var menu = UnityEngine.Object.FindAnyObjectByType<ShopMenu>(FindObjectsInactive.Include);
                if (menu == null)
                    menu = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(menuPath)))
                        .GetComponent<ShopMenu>();
                Set(menu, "inputModule", module);
                PrefabUtility.RecordPrefabInstancePropertyModifications(menu);

                var station = UnityEngine.Object.FindAnyObjectByType<ShopStation>(FindObjectsInactive.Include);
                if (station == null)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(stationPath));
                    obj.transform.SetParent(GameObject.Find("Environment").transform);
                    obj.transform.position = new Vector3(-3, 0, 0);
                    station = obj.GetComponent<ShopStation>();
                }
                Set(station, "menu", menu);
                PrefabUtility.RecordPrefabInstancePropertyModifications(station);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMenu(string path)
        {
            // Reuse M5's saved uGUI layout, font, button styles and explicit navigation.
            var root = PrefabUtility.LoadPrefabContents(Root + "Prefabs/UI/LevelingMenu.prefab");
            try
            {
                root.name = "ShopMenu";
                UnityEngine.Object.DestroyImmediate(root.GetComponent<LevelingMenu>());
                UnityEngine.Object.DestroyImmediate(root.GetComponentInChildren<EventSystem>(true).gameObject);
                var controller = root.AddComponent<ShopMenu>();
                var panel = root.transform.Find("Panel");
                var card = panel.Find("Card");
                card.GetComponent<RectTransform>().sizeDelta = new Vector2(660, 600);
                var title = card.Find("Title").GetComponent<UnityEngine.UI.Text>();
                title.text = "VILLAGE SHOP";
                Place(title.rectTransform, new Vector2(620, 44), new Vector2(0, 250));
                var details = card.Find("Details").GetComponent<UnityEngine.UI.Text>();
                details.text = "Gold: 0     HP: 5/5\n\nHealing - 5 gold\nRestore HP to maximum (no overheal).\n\n" +
                    "Max HP +1 - 25 gold\nLasts this session; current HP unchanged.";
                Place(details.rectTransform, new Vector2(620, 220), new Vector2(0, 116));
                var healing = card.Find("PlayerUpgrade").GetComponent<UnityEngine.UI.Button>();
                healing.name = "BuyHealing";
                healing.GetComponentInChildren<UnityEngine.UI.Text>().text = "Buy healing";
                healing.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -36);
                var maximum = card.Find("EnemyUpgrade").GetComponent<UnityEngine.UI.Button>();
                maximum.name = "BuyMaxHealth";
                maximum.GetComponentInChildren<UnityEngine.UI.Text>().text = "Buy +1 Max HP";
                maximum.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -96);
                var close = card.Find("Close").GetComponent<UnityEngine.UI.Button>();
                close.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -156);
                var feedback = card.Find("Feedback").GetComponent<UnityEngine.UI.Text>();
                feedback.text = "Choose an offer. Nothing is spent until you buy.";
                Place(feedback.rectTransform, new Vector2(620, 56), new Vector2(0, -218));
                var hint = UnityEngine.Object.Instantiate(feedback, card);
                hint.name = "Controls";
                hint.text = "Select: arrows / D-pad   Buy: Enter / A   Close: Esc / B";
                hint.fontSize = 18;
                Place(hint.rectTransform, new Vector2(640, 36), new Vector2(0, -272));
                Set(controller, "panel", panel.gameObject); Set(controller, "details", details);
                Set(controller, "feedback", feedback); Set(controller, "healingButton", healing);
                Set(controller, "maxHealthButton", maximum); Set(controller, "closeButton", close);
                panel.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void CreateStation(string path)
        {
            var root = new GameObject("ShopStation");
            try
            {
                root.layer = LayerMask.NameToLayer("World");
                var sprite = root.AddComponent<SpriteRenderer>();
                sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Sprites/Square.png");
                sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Settings/GreyboxUnlit.mat");
                sprite.color = new Color(.15f, .8f, .45f);
                sprite.sortingLayerName = "Props";
                sprite.size = new Vector2(1.2f, .85f);
                sprite.drawMode = SpriteDrawMode.Sliced;
                root.AddComponent<BoxCollider2D>().size = new Vector2(1.2f, .85f);
                root.AddComponent<ShopTransactions>();
                var station = root.AddComponent<ShopStation>();
                var sign = new GameObject("StationLabels", typeof(RectTransform), typeof(Canvas));
                sign.transform.SetParent(root.transform, false);
                sign.transform.localPosition = new Vector3(0, 1.1f, -.1f);
                sign.transform.localScale = Vector3.one * .01f;
                var canvas = sign.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingLayerName = "Foreground";
                canvas.sortingOrder = 5;
                sign.GetComponent<RectTransform>().sizeDelta = new Vector2(320, 100);
                var font = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/UI/LevelingMenu.prefab")
                    .GetComponentsInChildren<UnityEngine.UI.Text>(true).First().font;
                Label("Sign", sign.transform, "SHOP", 28, 0, font);
                var prompt = Label("InteractPrompt", sign.transform, "Hold E / Y / Triangle", 22, 43, font);
                prompt.gameObject.SetActive(false);
                Set(station, "interactionPrompt", prompt.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, string value, int size, float y, Font font)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            Place(text.rectTransform, new Vector2(320, 42), new Vector2(0, y));
            return text;
        }
    }
}
