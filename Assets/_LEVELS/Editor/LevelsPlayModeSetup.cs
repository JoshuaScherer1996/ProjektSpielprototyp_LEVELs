using UnityEditor;
using UnityEditor.SceneManagement;

namespace Levels.Editor
{
    /// <summary>Normal Editor runs start at the same main menu as the build.</summary>
    [InitializeOnLoad]
    public static class LevelsPlayModeSetup
    {
        public const string VillagePath = "Assets/_LEVELS/Scenes/Village.unity";
        public const string MainMenuPath = "Assets/_LEVELS/Scenes/MainMenu.unity";

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
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            EditorSceneManager.playModeStartScene = menu != null ? menu : AssetDatabase.LoadAssetAtPath<SceneAsset>(VillagePath);
        }

        // Validators may start in a combat scene for one run; returning to Edit mode restores MainMenu.
        public static void UseValidationScene(string path)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
}
