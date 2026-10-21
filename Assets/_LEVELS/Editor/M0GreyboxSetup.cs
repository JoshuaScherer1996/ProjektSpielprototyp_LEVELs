#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.EditorTools
{
    /// <summary>Explicit, editor-only authoring. Never runs on import or during gameplay.</summary>
    public static class M0GreyboxSetup
    {
        private const string Root = "Assets/_LEVELS";
        private const string MaterialPath = Root + "/Settings/GreyboxUnlit.mat";
        private const string WellPath = Root + "/Prefabs/Environment/Well.prefab";
        private const string DoorPath = Root + "/Prefabs/Environment/LockedBossDoor.prefab";
        private static readonly string[] SceneNames = { "Village", "Plateau", "Tower" };
        private static readonly string[] SortingNames = { "Ground", "Props", "Foreground" };
        private static readonly string[] Folders =
        {
            "Art/Sprites", "Audio", "Input", "Prefabs/Characters", "Prefabs/Environment",
            "Prefabs/Transitions", "Scenes", "Scripts/Player", "Scripts/Combat",
            "Scripts/Enemies", "Scripts/Scenes", "Scripts/Core", "Scripts/UI", "Settings", "Editor"
        };

        [MenuItem("Tools/LEVELs/M0/Create Missing Greybox")]
        private static void BuildFromMenu() => Debug.Log(Build());

        [MenuItem("Tools/LEVELs/M0/Validate Greybox")]
        private static void ValidateFromMenu() => Debug.Log(Validate());

        public static string Build()
        {
            GuardEditor();
            foreach (string folder in Folders) EnsureFolder(Root + "/" + folder);
            EnsureLayers();
            Sprite square = EnsureSprite("Square", false);
            Sprite circle = EnsureSprite("Circle", true);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                Require(shader != null, "URP Sprite-Unlit shader is unavailable.");
                material = new Material(shader) { name = "GreyboxUnlit" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            EnsurePrefab(WellPath, () => CreateWell(circle, material));
            EnsurePrefab(DoorPath, () => CreateDoor(square, material));
            for (int i = 0; i < SceneNames.Length; i++) EnsureScene(SceneNames[i], i, square, material);
            RegisterScenes();
            EditorSceneManager.OpenScene(ScenePath("Village"), OpenSceneMode.Single);
            return Validate();
        }

        private static void GuardEditor()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Stop Play Mode and wait for compilation before authoring M0.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save or discard your unsaved scene changes first.");
        }

        private static string ScenePath(string name) => Root + "/Scenes/" + name + ".unity";

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void EnsureLayers()
        {
            // Unity's serialized Editor API, never text edits of TagManager YAML.
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = manager.FindProperty("layers");
            if (LayerMask.NameToLayer("World") < 0)
            {
                int slot = Enumerable.Range(8, 24).FirstOrDefault(i => string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue));
                Require(slot >= 8, "No free user physics layer for World.");
                layers.GetArrayElementAtIndex(slot).stringValue = "World";
            }

            SerializedProperty sorting = manager.FindProperty("m_SortingLayers");
            foreach (string name in SortingNames)
            {
                int existingIndex = -1;
                var ids = new HashSet<int>();
                for (int i = 0; i < sorting.arraySize; i++)
                {
                    SerializedProperty layer = sorting.GetArrayElementAtIndex(i);
                    if (layer.FindPropertyRelative("name").stringValue == name) existingIndex = i;
                    ids.Add(layer.FindPropertyRelative("uniqueID").intValue);
                }
                if (existingIndex >= 0 && sorting.GetArrayElementAtIndex(existingIndex).FindPropertyRelative("uniqueID").intValue != 0) continue;
                int id;
                // SerializedProperty clamps negative int writes on this unsigned Unity field to zero.
                do { id = Guid.NewGuid().GetHashCode() & int.MaxValue; } while (id == 0 || ids.Contains(id));
                int index = existingIndex >= 0 ? existingIndex : sorting.arraySize;
                if (existingIndex < 0) sorting.InsertArrayElementAtIndex(index);
                SerializedProperty added = sorting.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("name").stringValue = name;
                added.FindPropertyRelative("uniqueID").intValue = id;
                added.FindPropertyRelative("locked").boolValue = false;
            }
            manager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(manager.targetObject);
        }

        private static Sprite EnsureSprite(string name, bool circle)
        {
            string path = Root + "/Art/Sprites/" + name + ".png";
            if (!File.Exists(path))
            {
                int size = circle ? 128 : 8;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                        float alpha = circle ? Mathf.Clamp01(size / 2f - distance) : 1f;
                        pixels[y * size + x] = new Color(1, 1, 1, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                var spriteSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(spriteSettings);
                spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(spriteSettings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, "Invalid sprite: " + path);
            return sprite;
        }

        private static GameObject Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Visual(string name, Transform parent, Sprite sprite, Material material,
            Vector2 position, Vector2 size, Color color, string sorting = "Props", int order = 0)
        {
            GameObject go = Group(name, parent);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingLayerName = sorting;
            renderer.sortingOrder = order;
            return go;
        }

        private static GameObject CreateWell(Sprite circle, Material material)
        {
            GameObject well = Group("Well");
            well.layer = LayerMask.NameToLayer("World");
            well.AddComponent<CircleCollider2D>().radius = 1.5f;
            Visual("StoneRim", well.transform, circle, material, Vector2.zero, Vector2.one * 3,
                new Color(0.64f, 0.65f, 0.67f));
            Visual("Water", well.transform, circle, material, Vector2.zero, Vector2.one * 2.25f,
                new Color(0.20f, 0.46f, 0.63f), order: 1);
            Visual("CenterStone", well.transform, circle, material, Vector2.zero, Vector2.one * 0.55f,
                new Color(0.75f, 0.76f, 0.78f), order: 2);
            return well;
        }

        private static GameObject CreateDoor(Sprite square, Material material)
        {
            GameObject door = Group("LockedBossDoor");
            door.layer = LayerMask.NameToLayer("World");
            door.AddComponent<BoxCollider2D>().size = new Vector2(6, 3);
            Visual("StoneFrame", door.transform, square, material, Vector2.zero, new Vector2(6, 3),
                new Color(0.39f, 0.40f, 0.43f));
            Visual("DoorPanel", door.transform, square, material, Vector2.zero, new Vector2(5.1f, 2.45f),
                new Color(0.12f, 0.13f, 0.16f), order: 1);
            Visual("CenterSeam", door.transform, square, material, Vector2.zero, new Vector2(0.07f, 2.45f),
                new Color(0.31f, 0.32f, 0.36f), order: 2);
            Color lockColor = new Color(0.77f, 0.61f, 0.30f);
            Visual("LockBar", door.transform, square, material, Vector2.zero, new Vector2(3.8f, 0.18f),
                lockColor, "Foreground");
            Visual("Lock", door.transform, square, material, Vector2.zero, new Vector2(0.65f, 0.75f),
                lockColor, "Foreground", 1);
            return door;
        }

        private static void EnsurePrefab(string path, Func<GameObject> create)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            // Preview scene keeps temporary authoring objects out of any user scene.
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject source = null;
            try
            {
                source = create();
                SceneManager.MoveGameObjectToScene(source, preview);
                Require(PrefabUtility.SaveAsPrefabAsset(source, path) != null, "Could not save prefab: " + path);
            }
            finally
            {
                if (source != null) Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void EnsureScene(string name, int index, Sprite square, Material material)
        {
            string path = ScenePath(name);
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Existing, non-empty scenes are never rebuilt or overwritten. Validate them instead.
            if (scene.rootCount != 0) return;

            Transform systems = Group("Systems").transform;
            GameObject cameraObject = Group("Main Camera", systems);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.085f, 0.10f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(-1);

            Transform environment = Group("Environment").transform;
            Color[] floors = { new Color(0.48f, 0.49f, 0.51f), new Color(0.29f, 0.48f, 0.29f), new Color(0.24f, 0.25f, 0.28f) };
            Visual("Ground", environment, square, material, Vector2.zero, new Vector2(24, 14), floors[index], "Ground");
            Transform boundaries = Group("WorldBounds", environment).transform;
            AddBoundary("North", boundaries, square, material, new Vector2(0, 7.25f), new Vector2(25, 0.5f));
            AddBoundary("South", boundaries, square, material, new Vector2(0, -7.25f), new Vector2(25, 0.5f));
            AddBoundary("West", boundaries, square, material, new Vector2(-12.25f, 0), new Vector2(0.5f, 14));
            AddBoundary("East", boundaries, square, material, new Vector2(12.25f, 0), new Vector2(0.5f, 14));
            Transform props = Group("Props", environment).transform;
            if (name == "Village") PlacePrefab(WellPath, props, Vector2.zero);
            if (name == "Tower") PlacePrefab(DoorPath, props, new Vector2(0, 5.1f));

            Transform markers = Group("Markers").transform;
            Transform spawns = Group("SpawnPoints", markers).transform;
            Transform transitions = Group("Transitions", markers).transform;
            if (name == "Village")
            {
                Marker("Spawn_Default", spawns, new Vector2(0, -4));
                Marker("Spawn_FromPlateau", spawns, new Vector2(0, 5));
                Marker("Transition_ToPlateau", transitions, new Vector2(0, 6.5f));
            }
            else if (name == "Plateau")
            {
                Marker("Spawn_FromVillage", spawns, new Vector2(0, -5));
                Marker("Spawn_FromTower", spawns, new Vector2(0, 5));
                Marker("Transition_ToVillage", transitions, new Vector2(0, -6.5f));
                Marker("Transition_ToTower", transitions, new Vector2(0, 6.5f));
            }
            else
            {
                Marker("Spawn_FromPlateau", spawns, new Vector2(0, -5));
                Marker("Transition_ToPlateau", transitions, new Vector2(0, -6.5f));
            }
            Require(EditorSceneManager.SaveScene(scene, path), "Could not save scene: " + path);
        }

        private static void AddBoundary(string name, Transform parent, Sprite sprite, Material material, Vector2 position, Vector2 size)
        {
            GameObject wall = Visual("Boundary_" + name, parent, sprite, material, position, size,
                new Color(0.14f, 0.16f, 0.19f));
            wall.layer = LayerMask.NameToLayer("World");
            wall.AddComponent<BoxCollider2D>();
        }

        private static void PlacePrefab(string path, Transform parent, Vector2 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            instance.transform.localPosition = position;
        }

        private static void Marker(string name, Transform parent, Vector2 position)
        {
            Group(name, parent).transform.localPosition = position;
        }

        private static EditorBuildSettingsScene[] OrderedScenes(EditorBuildSettingsScene[] existing)
        {
            string[] paths = SceneNames.Select(ScenePath).ToArray();
            return paths.Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(existing.Where(s => !paths.Contains(s.path))).ToArray();
        }

        private static void RegisterScenes()
        {
            EditorBuildSettings.scenes = OrderedScenes(EditorBuildSettings.scenes);
            BuildProfile profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null && profile.overrideGlobalScenes)
            {
                profile.scenes = OrderedScenes(profile.scenes);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
        }

        public static string Validate()
        {
            GuardEditor();
            foreach (string folder in Folders) Require(AssetDatabase.IsValidFolder(Root + "/" + folder), "Missing folder: " + folder);
            Require(LayerMask.NameToLayer("World") >= 8, "Missing World physics layer.");
            foreach (string name in SortingNames) Require(SortingLayer.layers.Any(l => l.name == name), "Missing sorting layer: " + name);
            Require(SortingLayer.GetLayerValueFromName("Ground") < SortingLayer.GetLayerValueFromName("Props") &&
                SortingLayer.GetLayerValueFromName("Props") < SortingLayer.GetLayerValueFromName("Foreground"), "Incorrect sorting order.");
            ValidatePrefab(WellPath, typeof(CircleCollider2D));
            ValidatePrefab(DoorPath, typeof(BoxCollider2D));
            var summary = new List<string>();
            foreach (string name in SceneNames)
            {
                string path = ScenePath(name);
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, "Missing saved scene: " + path);
                Scene scene = SceneManager.GetSceneByPath(path);
                bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
                if (openedForValidation) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameObject[] roots = scene.GetRootGameObjects();
                    var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                    Camera[] cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
                    Require(cameras.Length == 1 && cameras[0].orthographic, name + ": expected one orthographic camera.");
                    Require(roots.Any(r => r.name == "Systems") && roots.Any(r => r.name == "Environment") && roots.Any(r => r.name == "Markers"), name + ": hierarchy roots missing.");
                    Transform ground = transforms.Single(t => t.name == "Ground");
                    Require(ground.GetComponent<SpriteRenderer>()?.sortingLayerName == "Ground", name + ": invalid ground.");
                    Require(ground.GetComponent<Collider2D>() == null, name + ": ground must not block movement.");
                    Transform bounds = transforms.Single(t => t.name == "WorldBounds");
                    Require(bounds.childCount == 4, name + ": expected four boundaries.");
                    foreach (Collider2D collider in roots.SelectMany(r => r.GetComponentsInChildren<Collider2D>(true)))
                        Require(!collider.isTrigger && collider.gameObject.layer == LayerMask.NameToLayer("World"), name + ": invalid solid collider layer.");
                    foreach (Transform boundary in bounds)
                        Require(boundary.GetComponent<BoxCollider2D>() != null, name + ": boundary collider missing.");
                    foreach (Transform marker in transforms.Where(t => t.name.StartsWith("Spawn_") || t.name.StartsWith("Transition_")))
                        Require(marker.GetComponents<Component>().Length == 1, name + ": markers must be plain Transforms.");
                    string[] expected = name == "Village"
                        ? new[] { "Spawn_Default", "Spawn_FromPlateau", "Transition_ToPlateau" }
                        : name == "Plateau"
                            ? new[] { "Spawn_FromVillage", "Spawn_FromTower", "Transition_ToVillage", "Transition_ToTower" }
                            : new[] { "Spawn_FromPlateau", "Transition_ToPlateau" };
                    foreach (string marker in expected) Require(transforms.Any(t => t.name == marker), name + ": missing " + marker);
                    if (name == "Village" || name == "Tower")
                    {
                        string prefabPath = name == "Village" ? WellPath : DoorPath;
                        GameObject instance = transforms.Single(t => t.name == (name == "Village" ? "Well" : "LockedBossDoor")).gameObject;
                        Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) == prefabPath, name + ": prefab connection missing.");
                    }
                    foreach (GameObject root in roots)
                    {
                        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, name + ": missing script.");
                        Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(c => c is UniversalAdditionalCameraData), name + ": unexpected gameplay script.");
                        Require(root.GetComponentsInChildren<Rigidbody2D>(true).Length == 0, name + ": unexpected actor body in M0.");
                        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                            Require(renderer.sprite != null && renderer.sharedMaterial != null && !renderer.sharedMaterial.shader.name.Contains("InternalError"), name + ": missing render reference.");
                    }
                    foreach (string dependency in AssetDatabase.GetDependencies(path))
                        Require(File.Exists(dependency) || AssetDatabase.LoadMainAssetAtPath(dependency) != null, "Missing dependency: " + dependency);
                    summary.Add(name + ": " + transforms.Length + " objects, valid camera/ground/bounds/markers/references");
                }
                finally { if (openedForValidation) EditorSceneManager.CloseScene(scene, true); }
            }
            BuildProfile active = BuildProfile.GetActiveBuildProfile();
            EditorBuildSettingsScene[] effective = active != null && active.overrideGlobalScenes ? active.scenes : EditorBuildSettings.scenes;
            Require(effective.Length >= 3 && SceneNames.Select(ScenePath).SequenceEqual(effective.Take(3).Select(s => s.path)) && effective.Take(3).All(s => s.enabled), "Incorrect effective build scene order.");
            return "M0 validation passed. " + string.Join("; ", summary) + "; build order: Village, Plateau, Tower; profile: " + (active == null ? "platform/global scene list" : active.name);
        }

        private static void ValidatePrefab(string path, Type colliderType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Regular, "Missing regular prefab: " + path);
            Require(prefab.GetComponent(colliderType) != null && prefab.GetComponentsInChildren<SpriteRenderer>().All(r => r.sprite != null), "Invalid prefab contents: " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
