using System;
using System.Linq;
using Levels.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Levels.Editor
{
    public static class M8SpawnSetup
    {
        public const string PlateauPath = "Assets/_LEVELS/Scenes/Plateau.unity";
        public const string EnemyPath = "Assets/_LEVELS/Prefabs/Characters/Enemy_Basic.prefab";

        [MenuItem("LEVEL's/M8/Configure Plateau Spawner")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            Scene scene = SceneManager.GetSceneByPath(PlateauPath);
            if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Plateau has unsaved changes. Save before configuring M8.");
            var root = PrefabUtility.LoadPrefabContents(EnemyPath);
            try
            {
                var identity = root.GetComponent<EnemySpawnIdentity>() ?? root.AddComponent<EnemySpawnIdentity>();
                Set(identity, "category", "Goblin");
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            scene = SceneManager.GetSceneByPath(PlateauPath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PlateauPath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Plateau has unsaved changes. Save before configuring M8.");
            var spawners = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemySpawner>(true)).ToArray();
            if (spawners.Length > 1) throw new InvalidOperationException("Plateau has more than one spawner.");
            EnemySpawner spawner;
            if (spawners.Length == 1) spawner = spawners[0];
            else
            {
                var go = new GameObject("Plateau_EnemySpawner");
                SceneManager.MoveGameObjectToScene(go, scene);
                var systems = scene.GetRootGameObjects().First(g => g.name == "Systems");
                go.transform.SetParent(systems.transform, false);
                go.AddComponent<EnemySpawnArea>();
                spawner = go.AddComponent<EnemySpawner>();
            }
            var area = spawner.GetComponent<EnemySpawnArea>();
            Set(area, "center", Vector2.zero); Set(area, "size", new Vector2(22f, 12f));
            var so = new SerializedObject(spawner);
            so.FindProperty("spawnArea").objectReferenceValue = area;
            so.FindProperty("safeDistance").floatValue = 3f;
            so.FindProperty("entranceClearance").floatValue = 2f;
            so.FindProperty("colliderPadding").floatValue = .1f;
            so.FindProperty("attemptsPerInterval").intValue = 20;
            so.FindProperty("blockingLayers").intValue = LayerMask.GetMask("Default", "World", "Player", "Enemy");
            var entries = so.FindProperty("enemies"); entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("category").stringValue = "Goblin";
            entry.FindPropertyRelative("prefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath).GetComponent<EnemySpawnIdentity>();
            entry.FindPropertyRelative("maxAlive").intValue = 5;
            entry.FindPropertyRelative("spawnInterval").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            // All initial Goblins use the same validated random spawn path as replacements.
            // Remove only scene instances of the managed prefab, preserving unrelated actors.
            foreach (var enemy in scene.GetRootGameObjects()
                         .SelectMany(g => g.GetComponentsInChildren<EnemySpawnIdentity>(true)).ToArray())
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy.gameObject) == EnemyPath)
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = spawner.gameObject;
        }

        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (value is string text) p.stringValue = text;
            else if (value is Vector2 vector) p.vector2Value = vector;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
