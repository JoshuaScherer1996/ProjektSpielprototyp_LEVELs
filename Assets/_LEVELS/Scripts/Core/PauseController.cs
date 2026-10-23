using Levels.Player;
using Levels.Scenes;
using Levels.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Core
{
    /// <summary>One scene-owned pause owner. System/Pause stays available while gameplay/UI maps change.</summary>
    [DefaultExecutionOrder(-800), DisallowMultipleComponent]
    public sealed class PauseController : MonoBehaviour
    {
        [SerializeField] private GameFlow flow;
        [SerializeField] private PauseMenuUI menu;
        [SerializeField] private InputActionReference pauseAction;
        private InputAction pause;
        private bool requested;
        private bool held;
        private bool menuHadPriority;
        private int handledFrame = -1;

        private void OnEnable()
        {
            // Clone the existing asset action, not its maps: pause works from keyboard or any pad
            // regardless of PlayerInput's current pairing/binding mask. No gameplay action is disabled.
            pause = pauseAction.action.Clone();
            pause.performed += OnPause;
            pause.canceled += OnRelease;
            pause.Enable();
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            if (held) return;
            held = true;
            requested = true;
            menuHadPriority = HasStationMenu();
        }

        private void OnRelease(InputAction.CallbackContext context) => held = false;

        private static bool HasStationMenu()
        {
            foreach (var shop in FindObjectsByType<ShopMenu>()) if (shop.IsOpen) return true;
            foreach (var leveling in FindObjectsByType<LevelingMenu>()) if (leveling.IsOpen) return true;
            return false;
        }

        private void Update()
        {
            if (!requested) return;
            requested = false;
            if (handledFrame == Time.frameCount) return;
            handledFrame = Time.frameCount;
            if (SceneTransitionService.IsTransitioning || flow == null) return;
            // Capture priority at the input callback, before UI Cancel can close the station.
            if (menuHadPriority || HasStationMenu())
            {
                foreach (var shop in FindObjectsByType<ShopMenu>()) shop.Close();
                foreach (var leveling in FindObjectsByType<LevelingMenu>()) leveling.Close();
                PlayerInputReader.ConsumeInteractForCurrentFrame();
                return;
            }
            if (GameFlow.State == RunState.Paused) Continue();
            else if (flow.TryPause()) menu.Show();
        }

        public void Continue()
        {
            if (GameFlow.State != RunState.Paused || SceneTransitionService.IsTransitioning) return;
            menu.Hide();
            flow.Resume();
            handledFrame = Time.frameCount;
        }

        public void BackToMainMenu()
        {
            if (GameFlow.State == RunState.Paused && flow.BackToMainMenu()) menu.Hide();
        }

        private void OnDisable()
        {
            requested = held = menuHadPriority = false;
            if (pause != null)
            {
                pause.performed -= OnPause;
                pause.canceled -= OnRelease;
                pause.Dispose();
                pause = null;
            }
            if (menu != null) menu.Hide();
            if (flow != null && GameFlow.Instance == flow) flow.Resume();
        }
    }
}
