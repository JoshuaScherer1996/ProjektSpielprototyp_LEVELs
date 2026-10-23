using System;
using Levels.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    public static class M10FeedbackSetup
    {
        public const string BarPath = "Assets/_LEVELS/Prefabs/UI/EnemyHealthBar.prefab";
        public const string NumberPath = "Assets/_LEVELS/Prefabs/UI/FloatingDamageNumber.prefab";
        public const string FontPath = "Assets/_LEVELS/Art/Fonts/RetroDigits.asset";
        public const string PlayerPath = "Assets/_LEVELS/Prefabs/Characters/Player.prefab";
        public const string EnemyPath = M8SpawnSetup.EnemyPath;

        [MenuItem("LEVEL's/M10/Configure Damage Feedback")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before configuring feedback.");
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
                throw new InvalidOperationException("Import TMP Essential Resources before configuring M10.");
            if (!AssetDatabase.IsValidFolder("Assets/_LEVELS/Art/Fonts"))
                AssetDatabase.CreateFolder("Assets/_LEVELS/Art", "Fonts");
            TMP_FontAsset font = CreateFont();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BarPath) == null) CreateBar();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NumberPath) == null) CreateNumber(font);
            ConfigureActor(EnemyPath, true);
            ConfigureActor(PlayerPath, false);
            AssetDatabase.SaveAssets();
        }

        private static GameObject UIObject(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;
            return go;
        }

        private static Canvas WorldCanvas(GameObject root, int order)
        {
            root.transform.localScale = Vector3.one * .01f;
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Foreground";
            canvas.sortingOrder = order;
            return canvas;
        }

        private static void CreateBar()
        {
            GameObject root = UIObject("EnemyHealthBar", null, new Vector2(90, 10));
            try
            {
                Canvas canvas = WorldCanvas(root, 20);
                root.transform.localPosition = new Vector3(0, .64f, 0);
                var background = root.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(.055f, .045f, .06f, .97f);
                background.raycastTarget = false;
                GameObject inset = UIObject("Inset", root.transform, new Vector2(84, 4));
                GameObject fill = UIObject("HPFill", inset.transform, Vector2.zero);
                var rect = fill.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = fill.AddComponent<UnityEngine.UI.Image>();
                image.color = new Color(.47f, .88f, .23f, 1f);
                image.raycastTarget = false;
                var bar = root.AddComponent<EnemyHealthBar>();
                Set(bar, "fill", rect); Set(bar, "canvas", canvas);
                PrefabUtility.SaveAsPrefabAsset(root, BarPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateNumber(TMP_FontAsset font)
        {
            GameObject root = UIObject("FloatingDamageNumber", null, new Vector2(300, 50));
            try
            {
                WorldCanvas(root, 30);
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false; group.blocksRaycasts = false;
                GameObject textObject = UIObject("Damage", root.transform, new Vector2(300, 50));
                var text = textObject.AddComponent<TextMeshProUGUI>();
                text.font = font;
                text.fontSharedMaterial = font.material;
                text.fontSize = 32;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;
                text.richText = false;
                text.text = "1";
                text.color = new Color(1f, .79f, .16f);
                var number = root.AddComponent<FloatingDamageNumber>();
                Set(number, "label", text); Set(number, "opacity", group);
                PrefabUtility.SaveAsPrefabAsset(root, NumberPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ConfigureActor(string path, bool enemy)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (enemy && root.GetComponentInChildren<EnemyHealthBar>(true) == null)
                {
                    var bar = (GameObject)PrefabUtility.InstantiatePrefab(
                        AssetDatabase.LoadAssetAtPath<GameObject>(BarPath), root.transform);
                    bar.name = "EnemyHealthBar";
                }
                var feedback = root.GetComponent<DamageFeedback>() ?? root.AddComponent<DamageFeedback>();
                Set(feedback, "numberPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(NumberPath)
                    .GetComponent<FloatingDamageNumber>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Original 5x7 numerals, authored here. No OS font, external download or font license required.
        // A saved SDF atlas gives TMP's normal dark outline around the retro-shaped digits.
        private static TMP_FontAsset CreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;
            string[] digits = {
                "01110/10001/10011/10101/11001/10001/01110",
                "00100/01100/00100/00100/00100/00100/01110",
                "01110/10001/00001/00010/00100/01000/11111",
                "11110/00001/00001/01110/00001/00001/11110",
                "00010/00110/01010/10010/11111/00010/00010",
                "11111/10000/10000/11110/00001/00001/11110",
                "01110/10000/10000/11110/10001/10001/01110",
                "11111/00001/00010/00100/01000/01000/01000",
                "01110/10001/10001/01110/10001/10001/01110",
                "01110/10001/10001/01111/00001/00001/01110"
            };
            var font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = "RetroDigits";
            font.faceInfo = new FaceInfo {
                familyName = "LEVELS Retro Digits", styleName = "Regular", pointSize = 56,
                scale = 1, lineHeight = 72, ascentLine = 56, capLine = 56,
                meanLine = 32, baseline = 0, descentLine = -8
            };
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            var texture = new Texture2D(512, 256, TextureFormat.Alpha8, false) {
                name = "RetroDigits Atlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[512 * 256];
            for (int d = 0; d < 10; d++)
            {
                string[] rows = digits[d].Split('/');
                int x = (d % 5) * 64 + 12, y = (d / 5) * 80 + 12;
                for (int py = -9; py < 65; py++) for (int px = -9; px < 49; px++)
                {
                    float fx = (px + .5f) / 8f, fy = (py + .5f) / 8f;
                    int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                    bool inside = ix >= 0 && ix < 5 && iy >= 0 && iy < 7 && rows[6 - iy][ix] == '1';
                    float nearest = 10f;
                    for (int cy = -1; cy <= 7; cy++) for (int cx = -1; cx <= 5; cx++)
                    {
                        bool filled = cx >= 0 && cx < 5 && cy >= 0 && cy < 7 && rows[6 - cy][cx] == '1';
                        if (filled == inside) continue;
                        float dx = Mathf.Max(cx - fx, 0f, fx - cx - 1f);
                        float dy = Mathf.Max(cy - fy, 0f, fy - cy - 1f);
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dy * dy));
                    }
                    pixels[(y + py) * 512 + x + px] = new Color(1, 1, 1,
                        Mathf.Clamp01(.5f + (inside ? nearest : -nearest) * (8f / 18f)));
                }
                var glyph = new Glyph((uint)d + 1, new GlyphMetrics(40, 56, 0, 56, 48),
                    new GlyphRect(x, y, 40, 56), 1f, 0);
                font.glyphTable.Add(glyph);
                font.characterTable.Add(new TMP_Character((uint)('0' + d), font, glyph));
            }
            texture.SetPixels(pixels); texture.Apply(false, false);
            font.atlasTextures = new[] { texture };
            var so = new SerializedObject(font);
            so.FindProperty("m_Version").stringValue = "1.1.0";
            so.FindProperty("m_AtlasWidth").intValue = 512;
            so.FindProperty("m_AtlasHeight").intValue = 256;
            so.FindProperty("m_AtlasPadding").intValue = 8;
            so.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
            so.ApplyModifiedPropertiesWithoutUndo();
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            if (shader == null) throw new InvalidOperationException("TMP Distance Field shader missing.");
            var material = new Material(shader) { name = "RetroDigits Outline" };
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_TextureWidth", 512); material.SetFloat("_TextureHeight", 256);
            material.SetFloat("_GradientScale", 9);
            material.SetColor("_OutlineColor", new Color(.045f, .025f, .015f, 1f));
            material.SetFloat("_OutlineWidth", .18f);
            material.EnableKeyword("OUTLINE_ON");
            font.material = material;
            font.ReadFontAssetDefinition();
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.AddObjectToAsset(material, font);
            EditorUtility.SetDirty(font);
            return font;
        }
    }
}
