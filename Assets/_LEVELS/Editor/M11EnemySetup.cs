using System;
using System.Linq;
using Levels.Core;
using Levels.Enemies;
using Levels.Player;
using Levels.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Edits the saved M11 assets through Unity. Never runs automatically or in a player.</summary>
    public static class M11EnemySetup
    {
        public const string Root = "Assets/_LEVELS/";
        public const string GoblinPath = Root + "Prefabs/Characters/Enemy_Goblin.prefab";
        public const string CyclopsPath = Root + "Prefabs/Characters/Enemy_Cyclops.prefab";
        public const string GoblinGuid = "ab0967e253465da43a841bc0b15404c2";
        public const float CyclopsTargetSpeed = 1.4f;

        [MenuItem("LEVEL's/M11/Configure Enemy Types")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            string oldPath = AssetDatabase.GUIDToAssetPath(GoblinGuid);
            if (string.IsNullOrEmpty(oldPath)) throw new InvalidOperationException("Original Goblin GUID is missing.");
            if (oldPath != GoblinPath)
            {
                string error = AssetDatabase.MoveAsset(oldPath, GoblinPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            Edit(GoblinPath, root =>
            {
                root.name = "Enemy_Goblin";
                Set(root.GetComponent<EnemyProgression>(), "enemyType", (int)EnemyType.Goblin);
            });
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CyclopsPath) == null && !AssetDatabase.CopyAsset(GoblinPath, CyclopsPath))
                throw new InvalidOperationException("Cannot copy Goblin into independent Cyclops prefab.");
            Edit(CyclopsPath, root =>
            {
                root.name = "Enemy_Cyclops";
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.color = new Color(.48f, .27f, .12f);
                renderer.size = new Vector2(1.2f, 1.45f);
                root.GetComponent<BoxCollider2D>().size = renderer.size;
                Set(root.GetComponent<EnemyHealth>(), "maxHealth", 8);
                Set(root.GetComponent<ContactDamage>(), "damage", 3);
                Set(root.GetComponent<EnemyReward>(), "experience", 25);
                Set(root.GetComponent<EnemyReward>(), "gold", 15);
                Set(root.GetComponent<EnemySpawnIdentity>(), "category", "Cyclops");
                Set(root.GetComponent<EnemyProgression>(), "enemyType", (int)EnemyType.Cyclops);
                var ai = root.GetComponent<EnemyAI>();
                Set(ai, "persistentAggro", true);
                Set(ai, "usePlayerSpeed", true);
                var player = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Characters/Player.prefab");
                Set(ai, "playerSpeedRatio", CyclopsTargetSpeed / player.GetComponent<PlayerMovement>().MoveSpeed);
                Set(ai, "moveSpeed", CyclopsTargetSpeed);
                Eye(root, "Eye", .62f, Color.white, 1, renderer);
                Eye(root, "Pupil", .26f, new Color(.08f, .055f, .035f), 2, renderer);
                var bar = root.GetComponentInChildren<EnemyHealthBar>(true);
                bar.transform.localPosition = new Vector3(0, 1.05f, 0);
                Set(root.GetComponent<DamageFeedback>(), "offset", new Vector3(0, 1.35f, 0));
            });
            // Persist the new independent tuning values in the existing progression asset.
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<ProgressionSettings>(Root + "Resources/LevelsProgression.asset"));
            ConfigureMenu();
            ConfigureHUD();
            ConfigureSpawner();
            AssetDatabase.SaveAssets();
            Debug.Log("M11: Goblin GUID preserved; editable Cyclops, three level purchases and two spawn categories configured.");
        }

        private static void Eye(GameObject root, string name, float size, Color color, int order, SpriteRenderer body)
        {
            Transform child = root.transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
            child.gameObject.layer = root.layer;
            child.localPosition = new Vector3(0, .23f, -.01f * order);
            child.localScale = Vector3.one * size;
            var sprite = child.GetComponent<SpriteRenderer>();
            if (sprite == null) sprite = child.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/Sprites/Circle.png");
            sprite.sharedMaterial = body.sharedMaterial;
            sprite.color = color;
            sprite.sortingLayerID = body.sortingLayerID;
            sprite.sortingOrder = body.sortingOrder + order;
        }

        private static void ConfigureMenu() => Edit(Root + "Prefabs/UI/LevelingMenu.prefab", root =>
        {
            Transform card = root.transform.Find("Panel/Card");
            var player = card.Find("PlayerUpgrade").GetComponent<UnityEngine.UI.Button>();
            var goblin = card.Find("EnemyUpgrade").GetComponent<UnityEngine.UI.Button>();
            Transform cyclopsTransform = card.Find("CyclopsUpgrade");
            if (cyclopsTransform == null)
            {
                cyclopsTransform = Object.Instantiate(goblin.gameObject, card).transform;
                cyclopsTransform.name = "CyclopsUpgrade";
            }
            var cyclops = cyclopsTransform.GetComponent<UnityEngine.UI.Button>();
            var close = card.Find("Close").GetComponent<UnityEngine.UI.Button>();
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(640, 650);
            Rect(card.Find("Title"), new Vector2(600, 44), 275);
            Rect(card.Find("Details"), new Vector2(600, 184), 148);
            var buttons = new[] { player, goblin, cyclops, close };
            string[] labels = { "LEVEL UP PLAYER", "LEVEL UP GOBLIN", "LEVEL UP CYCLOPS", "CLOSE" };
            for (int i = 0; i < buttons.Length; i++)
            {
                Rect(buttons[i].transform, new Vector2(420, 48), 15 - i * 60);
                buttons[i].GetComponentInChildren<UnityEngine.UI.Text>(true).text = labels[i];
                buttons[i].navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length]
                };
            }
            Rect(card.Find("Feedback"), new Vector2(600, 70), -255);
            card.Find("Details").GetComponent<UnityEngine.UI.Text>().text =
                "Player Lv 1   Goblin Lv 1   Cyclops Lv 1\nAvailable EXP: 0\n\nPlayer upgrade: 20 EXP\nGoblin upgrade: 20 EXP\nCyclops upgrade: 20 EXP";
            Set(root.GetComponent<LevelingMenu>(), "cyclopsButton", cyclops);
        });

        private static void ConfigureHUD() => Edit(Root + "Prefabs/UI/PlayerHUD.prefab", root =>
        {
            var hud = new SerializedObject(root.GetComponent<PlayerHealthHUD>());
            var text = (UnityEngine.UI.Text)hud.FindProperty("levelText").objectReferenceValue;
            text.text = "Player Lv 1  |  Goblin Lv 1  |  Cyclops Lv 1";
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = text.fontSize;
            // The label stretches to its existing panel; widen that panel rather than adding to stretch width.
            var panel = text.transform.parent.GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(600, panel.sizeDelta.y);
        });

        private static void ConfigureSpawner()
        {
            var scene = SceneManager.GetSceneByPath(M8SpawnSetup.PlateauPath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(M8SpawnSetup.PlateauPath, OpenSceneMode.Additive);
            try
            {
                var spawner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemySpawner>(true)).Single();
                var so = new SerializedObject(spawner);
                var entries = so.FindProperty("enemies"); entries.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("category").stringValue = i == 0 ? "Goblin" : "Cyclops";
                    entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                        i == 0 ? GoblinPath : CyclopsPath).GetComponent<EnemySpawnIdentity>();
                    entry.FindPropertyRelative("maxAlive").intValue = i == 0 ? 5 : 2;
                    entry.FindPropertyRelative("spawnInterval").floatValue = i == 0 ? 2f : 3f;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void Rect(Transform target, Vector2 size, float y)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0, y);
        }
        private static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(target.name + ": missing " + field);
            if (value is int number) p.intValue = number;
            else if (value is float scalar) p.floatValue = scalar;
            else if (value is bool flag) p.boolValue = flag;
            else if (value is string text) p.stringValue = text;
            else if (value is Vector3 vector) p.vector3Value = vector;
            else if (value is Object reference) p.objectReferenceValue = reference;
            else throw new ArgumentException(field);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
