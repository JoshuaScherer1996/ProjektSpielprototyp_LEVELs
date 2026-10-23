using System;
using System.Linq;
using Levels.Core;
using Levels.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Levels.Editor
{
    public static class M13PauseSetup
    {
        public const string PrefabPath = M12MenuSetup.Root + "Prefabs/UI/PauseMenu.prefab";

        [MenuItem("LEVEL's/M13/Configure Pause Menu")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
                foreach (string name in new[] { "Village", "Plateau", "Tower" })
                {
                    var scene = EditorSceneManager.OpenScene(M12MenuSetup.Root + "Scenes/" + name + ".unity");
                    var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
                    var pause = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions)).OfType<InputActionReference>()
                        .First(r => (r.hideFlags & HideFlags.HideInHierarchy) == 0 && r.action.id == actions.FindAction("System/Pause", true).id);
                    var flow = Object.FindAnyObjectByType<GameFlow>();
                    var module = Object.FindAnyObjectByType<InputSystemUIInputModule>();
                    var controller = flow.GetComponent<PauseController>();
                    if (controller == null) controller = flow.gameObject.AddComponent<PauseController>();
                    var ui = Object.FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
                    if (ui == null) ui = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))).GetComponent<PauseMenuUI>();
                    M12MenuSetup.Set(controller, "flow", flow);
                    M12MenuSetup.Set(controller, "menu", ui);
                    M12MenuSetup.Set(controller, "pauseAction", pause);
                    M12MenuSetup.Set(ui, "controller", controller);
                    M12MenuSetup.Set(ui.GetComponentInChildren<MenuFocus>(true), "inputModule", module);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            Debug.Log("M13: editable PauseMenu prefab and pause controller saved in Village, Plateau and Tower.");
        }

        private static void CreatePrefab()
        {
            // Reuse M12's authored pixel typography, button navigation, focus and responsive Canvas.
            var root = PrefabUtility.LoadPrefabContents(M12MenuSetup.GameOverPath);
            try
            {
                root.name = "PauseMenu";
                root.GetComponent<Canvas>().sortingOrder = 150;
                Object.DestroyImmediate(root.GetComponent<GameOverUI>());
                var panel = root.transform.Find("Panel").gameObject;
                panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .035f, .055f, .80f);
                var title = panel.transform.Find("Content/Title").GetComponent<TextMeshProUGUI>();
                title.text = "PAUSED"; title.color = new Color(.94f, .76f, .39f);
                var resume = panel.transform.Find("Content/Retry").GetComponent<UnityEngine.UI.Button>();
                resume.name = "Continue";
                resume.GetComponentInChildren<TextMeshProUGUI>().text = "CONTINUE";
                var back = panel.transform.Find("Content/BackToMainMenu").GetComponent<UnityEngine.UI.Button>();
                var ui = root.AddComponent<PauseMenuUI>();
                M12MenuSetup.Set(ui, "panel", panel);
                M12MenuSetup.Set(ui, "continueButton", resume);
                M12MenuSetup.Set(ui, "mainMenuButton", back);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
