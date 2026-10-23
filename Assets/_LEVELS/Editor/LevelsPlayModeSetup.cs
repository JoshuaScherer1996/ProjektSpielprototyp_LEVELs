using UnityEditor;
using UnityEditor.SceneManagement;

namespace Levels.Editor
{
    /// <summary>Normal Editor runs always start in the hub, regardless of the scene being edited.</summary>
    [InitializeOnLoad]
    public static class LevelsPlayModeSetup
    {
        public const string VillagePath = "Assets/_LEVELS/Scenes/Village.unity";

        static LevelsPlayModeSetup()
        {
            EditorApplication.delayCall += RestoreVillageStart;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) RestoreVillageStart();
            };
        }

        public static void RestoreVillageStart()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(VillagePath);
        }

        // Validators may start in a combat scene for one run; returning to Edit mode restores Village.
        public static void UseValidationScene(string path)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
}
