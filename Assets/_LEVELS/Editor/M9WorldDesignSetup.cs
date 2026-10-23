using System;
using System.IO;
using System.Linq;
using Levels.Enemies;
using Levels.Player;
using Levels.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    /// <summary>Explicit authoring only: all textures, geometry and UI are saved assets.</summary>
    public static class M9WorldDesignSetup
    {
        public const string Root = "Assets/_LEVELS/";
        private const string Art = Root + "Art/Sprites/";
        private const string Props = Root + "Prefabs/Environment/";
        private static Material material;
        private static Font font;

        [MenuItem("LEVEL's/M9/Author World Design")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Settings/GreyboxUnlit.mat");
            font = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/UI/LevelingMenu.prefab")
                .GetComponentsInChildren<UnityEngine.UI.Text>(true).First().font;
            Sprite gate = Texture("M9_StoneGate", 96, 64, 16, GatePixel);
            Sprite tower = Texture("M9_RoundTower", 112, 80, 16, TowerPixel);
            Sprite cliff = Texture("M9_BrownCliff", 24, 24, 16, CliffPixel);
            Sprite light = Texture("M9_ExitLight", 64, 64, 16, LightPixel);
            SavePrefab("VillageGate", () => Gate(gate));
            SavePrefab("TowerEntrance", () => Tower(tower));
            SavePrefab("PlateauCliffs", () => Cliffs(cliff));
            SavePrefab("TowerExit", () => Exit(light));
            ConfigureDoor();
            try
            {
                foreach (string name in new[] { "Village", "Plateau", "Tower" })
                {
                    Scene scene = EditorSceneManager.OpenScene(Root + "Scenes/" + name + ".unity", OpenSceneMode.Single);
                    var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                    Transform environment = transforms.Single(t => t.name == "Environment");
                    Transform bounds = transforms.Single(t => t.name == "WorldBounds");
                    if (name == "Plateau")
                    {
                        foreach (Transform child in bounds.Cast<Transform>().ToArray())
                            if (child.name.StartsWith("Boundary_", StringComparison.Ordinal)) Object.DestroyImmediate(child.gameObject);
                        Place("PlateauCliffs", bounds, Vector2.zero);
                        var area = transforms.Select(t => t != null ? t.GetComponent<EnemySpawnArea>() : null).Single(a => a != null);
                        var settings = new SerializedObject(area);
                        settings.FindProperty("center").vector2Value = Vector2.zero;
                        // Keep even the editor rectangle clear of the south gate's inward pillars.
                        settings.FindProperty("size").vector2Value = new Vector2(22f,9f);
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else SplitBoundary(bounds, name == "Village" ? "Boundary_North" : "Boundary_South");
                    foreach (ScenePortal portal in transforms.Where(t => t != null).Select(t => t.GetComponent<ScenePortal>()).Where(p => p != null))
                    {
                        // The existing trigger and service references remain the sole transition path.
                        var renderer = portal.GetComponent<SpriteRenderer>();
                        if (renderer != null) renderer.enabled = false;
                        string prefab = name == "Tower" ? "TowerExit" : portal.DestinationScene == "Tower" ? "TowerEntrance" : "VillageGate";
                        var instance = Place(prefab, environment, portal.transform.position);
                        instance.name = "M9_" + prefab + "_To" + portal.DestinationScene;
                        if (name == "Plateau" && portal.DestinationScene == "Village")
                            HideReturnArch(instance);
                        var sign = instance.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t => t.name == "Destination");
                        sign.text = portal.DestinationScene == "Village" ? "VILLAGE  v" : portal.DestinationScene == "Tower" ? "TOWER  ^" :
                            name == "Tower" ? "PLATEAU  v" : "PLATEAU  ^";
                        PrefabUtility.RecordPrefabInstancePropertyModifications(sign);
                        AddApproach(environment, portal.transform.position.y > 0 ? 1 : -1);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            AssetDatabase.SaveAssets();
            Debug.Log("M9 world prefabs and all three scenes saved. No runtime world generation.");
        }

        private static Color Stone(int x, int y, Color main)
        {
            int row = y / 8, stagger = (row % 2) * 8;
            if (y % 8 == 0 || (x + stagger) % 16 == 0) return main * .58f;
            float shade = y % 8 == 7 ? 1.22f : ((x * 13 + y * 7) % 29 == 0 ? .85f : 1f);
            return new Color(main.r * shade, main.g * shade, main.b * shade, 1);
        }

        private static Color GatePixel(int x, int y)
        {
            bool pillar = (x < 24 || x >= 72) && y < 48;
            float dx = (x - 47.5f) / 40f, dy = (y - 28f) / 31f;
            bool arch = y >= 28 && dx * dx + dy * dy < 1.22f && (Mathf.Abs(x - 47.5f) >= 24 || y >= 48);
            if (!pillar && !arch) return Color.clear;
            Color c = Stone(x, y, new Color(.49f, .47f, .40f));
            if (y < 4 || (y >= 43 && y < 47)) c = new Color(.66f, .61f, .49f);
            return c;
        }

        private static Color TowerPixel(int x, int y)
        {
            float dx = (x - 55.5f) / 55f, dy = (y - 41f) / 40f;
            if (dx * dx + dy * dy > 1f) return Color.clear;
            if (x >= 32 && x < 80 && y < 40) return Color.clear;
            var c = Stone(x, y, new Color(.31f, .33f, .36f));
            float light = 1f - Mathf.Abs(dx) * .35f;
            c = new Color(c.r * light, c.g * light, c.b * light, 1);
            if ((x >= 21 && x < 27 || x >= 85 && x < 91) && y >= 45 && y < 60) c = new Color(.08f,.09f,.11f);
            return c;
        }

        private static Color CliffPixel(int x, int y)
        {
            // Stepped rock shelves and diagonal fractures, rather than masonry joints.
            float facet = ((x / 6 + y / 5) % 3) * .035f;
            var c = new Color(.45f + facet, .28f + facet, .145f + facet);
            int shelf = 20 + (x / 5 % 2);
            if (y >= shelf) c = new Color(.64f, .43f, .23f);
            else if (y == shelf - 1 || y == 5 + x / 9 || y == 12 + (x / 5 % 3) ||
                     (x == 6 + y / 4 || x == 20 - y / 6) && y < 19)
                c = new Color(.28f, .16f, .08f);
            else if (y < 3) c = new Color(.33f, .19f, .095f);
            else if ((x * 7 + y * 11) % 43 == 0) c = new Color(.61f, .39f, .20f);
            return c;
        }

        private static Color LightPixel(int x, int y)
        {
            float t = y / 63f;
            float width = Mathf.Lerp(11f, 30f, t);
            if (Mathf.Abs(x - 31.5f) > width) return Color.clear;
            float edge = Mathf.Clamp01((width - Mathf.Abs(x - 31.5f)) / 4f);
            float alpha = Mathf.Floor(Mathf.Lerp(.65f, 0f, t) * edge * 8f) / 8f;
            return new Color(1f, .91f, .63f, alpha);
        }

        private static Sprite Texture(string name, int width, int height, int ppu, Func<int,int,Color> pixel)
        {
            string path = Art + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) texture.SetPixel(x, y, pixel(x,y));
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject Group(string name, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
        }
        private static SpriteRenderer Visual(string name, Transform parent, Sprite sprite, Vector2 pos, string sorting, int order = 0)
        {
            var go = Group(name, parent); go.transform.localPosition = pos;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = material; renderer.sortingLayerName = sorting; renderer.sortingOrder = order;
            return renderer;
        }
        private static void Box(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = Group(name, parent); go.layer = LayerMask.NameToLayer("World"); go.transform.localPosition = pos;
            go.AddComponent<BoxCollider2D>().size = size;
        }
        private static GameObject Gate(Sprite sprite)
        {
            var root = Group("VillageGate");
            Visual("StoneArch", root.transform, sprite, new Vector2(0,.75f), "Foreground");
            Box("WestPillar", root.transform, new Vector2(-2.25f,.25f), new Vector2(1.5f,3));
            Box("EastPillar", root.transform, new Vector2(2.25f,.25f), new Vector2(1.5f,3));
            Sign(root.transform, new Vector2(0,2.4f), "PLATEAU  ^");
            return root;
        }
        private static GameObject Tower(Sprite sprite)
        {
            var root = Group("TowerEntrance");
            Visual("RoundedStoneFacade", root.transform, sprite, new Vector2(0,1.25f), "Foreground");
            // Rounded outer silhouette, straight jambs on the three-unit open passage.
            foreach (int side in new[] {-1,1})
            {
                var go = Group(side < 0 ? "WestStoneWall" : "EastStoneWall", root.transform);
                go.layer = LayerMask.NameToLayer("World");
                go.AddComponent<PolygonCollider2D>().points = new[] {
                    new Vector2(side*1.5f,-.9f), new Vector2(side*2.4f,-.5f), new Vector2(side*3.25f,.4f),
                    new Vector2(side*3.5f,1.25f), new Vector2(side*3.25f,2.2f),
                    new Vector2(side*2.4f,3.1f),new Vector2(side*1.5f,3.5f)};
            }
            Sign(root.transform, new Vector2(0,3.9f), "TOWER  ^"); return root;
        }
        private static GameObject Cliffs(Sprite sprite)
        {
            var root = Group("PlateauCliffs");
            foreach (int side in new[] {-1,1})
            {
                CliffWall(root.transform, sprite, side < 0 ? "West" : "East", new Vector2(side*12.75f,0), new Vector2(1.5f,17));
                foreach (int half in new[] {-1,1})
                    CliffWall(root.transform, sprite, (side < 0 ? "South" : "North") + (half < 0 ? "West" : "East"),
                        new Vector2(half*7.5f,side*7.75f), new Vector2(12,1.5f));
                // ScenePortal accepts players only. Seal its outer world edge for enemies,
                // while explicitly excluding the Player layer so the passage stays walkable.
                Box(side < 0 ? "SouthEnemyLimit" : "NorthEnemyLimit",root.transform,
                    new Vector2(0,side*7.75f),new Vector2(3,1.5f));
                root.transform.GetChild(root.transform.childCount-1).GetComponent<BoxCollider2D>().excludeLayers = LayerMask.GetMask("Player");
            }
            return root;
        }
        private static void CliffWall(Transform root, Sprite sprite, string name, Vector2 pos, Vector2 size)
        {
            var renderer = Visual(name, root, sprite, pos, "Props");
            renderer.drawMode = SpriteDrawMode.Tiled; renderer.size = size;
            renderer.gameObject.layer = LayerMask.NameToLayer("World");
            renderer.gameObject.AddComponent<BoxCollider2D>().size = size;
        }
        private static GameObject Exit(Sprite light)
        {
            var root = Group("TowerExit");
            Sign(root.transform, new Vector2(0,2.4f), "PLATEAU  v");
            Visual("IncomingLight", root.transform, light, new Vector2(0,2f), "Ground", 10);
            var threshold = Visual("BrightThreshold", root.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Square.png"), Vector2.zero, "Ground", 9);
            threshold.transform.localScale = new Vector3(3f,.5f,1); threshold.color = new Color(1,.91f,.65f,.75f);
            return root;
        }
        private static void HideReturnArch(GameObject root)
        {
            foreach (string name in new[] { "StoneArch", "WestPillar", "EastPillar" })
            {
                var child = root.transform.Find(name);
                if (child == null) continue;
                child.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }

        [MenuItem("LEVEL's/M9/Open Return Exits")]
        public static void OpenReturnExits()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var root = PrefabUtility.LoadPrefabContents(Props + "TowerExit.prefab");
            try
            {
                foreach (string name in new[] { "StoneArch", "WestPillar", "EastPillar" })
                {
                    var child = root.transform.Find(name);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root,Props + "TowerExit.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            try
            {
                var scene = EditorSceneManager.OpenScene(Root + "Scenes/Plateau.unity");
                var gate = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                    .Single(t=>t.name=="M9_VillageGate_ToVillage");
                HideReturnArch(gate.gameObject);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            AssetDatabase.SaveAssets();
        }
        private static void Sign(Transform root, Vector2 pos, string value)
        {
            var go = new GameObject("DestinationSign", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(root,false); go.transform.localPosition = new Vector3(pos.x,pos.y,-.1f);
            go.transform.localScale = Vector3.one * .01f;
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Foreground"; canvas.sortingOrder = 20;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(500,60);
            Text("Destination",go.transform,value,30,new Vector2(500,60),Vector2.zero);
        }
        private static UnityEngine.UI.Text Text(string name, Transform parent, string value, int size, Vector2 dimensions, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent,false);
            var text = go.AddComponent<UnityEngine.UI.Text>(); text.font = font; text.text = value;
            text.fontSize = size; text.fontStyle = FontStyle.Bold; text.color = new Color(1f,.94f,.76f);
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f,.5f);
            text.rectTransform.sizeDelta = dimensions; text.rectTransform.anchoredPosition = pos;
            var shadow = go.AddComponent<UnityEngine.UI.Shadow>(); shadow.effectColor = new Color(0,0,0,.95f);
            shadow.effectDistance = new Vector2(2,-2); return text;
        }
        private static void ConfigureDoor()
        {
            string path = Props + "LockedBossDoor.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var interaction = root.GetComponent<LockedBossDoorInteraction>() ?? root.AddComponent<LockedBossDoorInteraction>();
                var old = root.transform.Find("M9_DoorUI"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var ui = Group("M9_DoorUI", root.transform);
                Sign(ui.transform, new Vector2(0,2f), "BOSSTUER - VERSIEGELT");
                var prompt = Text("InteractPrompt",ui.transform.GetChild(0),"E / A : Untersuchen",24,new Vector2(500,50),new Vector2(0,-45));
                prompt.gameObject.SetActive(false);
                var canvasObject = new GameObject("MessageCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
                canvasObject.transform.SetParent(ui.transform,false);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
                var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
                var panel = new GameObject("MessagePanel",typeof(RectTransform),typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(canvasObject.transform,false);
                var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.08f,.08f); rect.anchorMax = new Vector2(.92f,.36f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = panel.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.055f,.06f,.08f,.97f); image.raycastTarget = false;
                var body = Text("BossMessage",panel.transform,"Der Boss schläft noch.\nKomm in Phase 3 wieder!",30,new Vector2(900,100),new Vector2(0,18));
                body.rectTransform.anchorMin = new Vector2(.03f,.35f); body.rectTransform.anchorMax = new Vector2(.97f,.95f);
                body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;
                Text("CloseHint",panel.transform,"E / A / Esc: Schließen   |   schließt nach 4 Sekunden",18,new Vector2(900,32),new Vector2(0,-65));
                panel.SetActive(false);
                var so = new SerializedObject(interaction);
                so.FindProperty("blockingCollider").objectReferenceValue = root.GetComponent<Collider2D>();
                so.FindProperty("interactionPrompt").objectReferenceValue = prompt.gameObject;
                so.FindProperty("messagePanel").objectReferenceValue = panel;
                so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void SavePrefab(string name, Func<GameObject> create)
        {
            var preview = EditorSceneManager.NewPreviewScene(); GameObject root = null;
            try { root = create(); SceneManager.MoveGameObjectToScene(root,preview); PrefabUtility.SaveAsPrefabAsset(root,Props+name+".prefab"); }
            finally { if (root != null) Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static GameObject Place(string name, Transform parent, Vector2 pos)
        {
            string path = Props + name + ".prefab";
            var existing = parent.Cast<Transform>().FirstOrDefault(t =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == path && Vector2.Distance(t.position,pos)<.01f);
            if (existing != null) return existing.gameObject;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent);
            go.transform.position = pos; return go;
        }
        private static void SplitBoundary(Transform parent, string name)
        {
            var original = parent.Find(name); if (original == null) return;
            float y = original.position.y;
            original.position = new Vector3(-7,y,0); original.localScale = new Vector3(11,.5f,1);
            if (parent.Find(name+"_East") == null)
            {
                var east = Object.Instantiate(original.gameObject,parent); east.name = name+"_East"; east.transform.position = new Vector3(7,y,0);
            }
        }
        private static void AddApproach(Transform environment, int direction)
        {
            string name = "M9_Approach_"+direction; if (environment.Find(name) != null) return;
            var renderer = Visual(name,environment,AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Square.png"),
                new Vector2(0,direction*5.5f),"Ground",2);
            renderer.transform.localScale = new Vector3(2.8f,3,1); renderer.color = new Color(.52f,.45f,.30f);
            for (int i = 0; i < 3; i++)
            {
                var mark = Visual("Waystone_"+i,renderer.transform,AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Square.png"),
                    new Vector2((i%2==0 ? -.16f : .16f), (i-1)*.25f),"Ground",3);
                mark.transform.localScale = new Vector3(.25f,.08f,1); mark.color = new Color(.72f,.65f,.47f);
            }
        }
    }
}
