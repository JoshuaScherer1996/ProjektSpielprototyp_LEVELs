using System.Collections;
using System.Collections.Generic;
using Levels.Core;
using Levels.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Levels.UI
{
    /// <summary>Shared by the two run menus; uses the scene's one existing UI input module.</summary>
    public sealed class MenuFocus : MonoBehaviour
    {
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private UnityEngine.UI.Button firstButton;
        private bool ready;
        private bool pointerMode;
        private Vector2 lastPointerPosition;
        private InputAction navigate;
        private readonly List<RaycastResult> pointerHits = new();

        private void OnEnable()
        {
            ready = false;
            navigate = inputModule.move.action;
            navigate.performed += OnNavigate;
            StartCoroutine(Arm());
        }
        private IEnumerator Arm()
        {
            inputModule.enabled = false;
            // A/Enter/click that caused death or a scene change must be released before Submit.
            yield return null;
            while (GameFlow.State == RunState.Loading || SceneTransitionService.IsTransitioning || SubmitHeld())
                yield return null;
            // Project-wide UI actions may still be enabled after the module resets its pointer state.
            // Re-arm Point's existing initial-state check so a stationary mouse can click immediately.
            inputModule.point.action.Disable();
            inputModule.enabled = true;
            ready = true;
            pointerMode = false;
            lastPointerPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            SelectFirst();
            if (Mouse.current != null && HoveredButton(lastPointerPosition) != null)
            {
                pointerMode = true;
                SelectHovered(lastPointerPosition);
            }
        }

        private static bool SubmitHeld() =>
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) ||
            (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed)) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed);

        private void Update()
        {
            if (!ready) return;
            if (GameFlow.State == RunState.Loading) { inputModule.enabled = false; ready = false; StartCoroutine(Arm()); return; }
            if (Mouse.current == null) return;
            Vector2 position = Mouse.current.position.ReadValue();
            if ((position - lastPointerPosition).sqrMagnitude > .01f) pointerMode = true;
            lastPointerPosition = position;
            if (pointerMode) SelectHovered(position);
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            if (!ready || context.ReadValue<Vector2>().sqrMagnitude <= .01f) return;
            pointerMode = false;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null) SelectFirst();
        }

        private UnityEngine.UI.Button HoveredButton(Vector2 position)
        {
            if (EventSystem.current == null) return null;
            pointerHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, pointerHits);
            foreach (var hit in pointerHits)
            {
                var button = hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                if (button != null && button.transform.IsChildOf(transform) && button.IsActive() && button.IsInteractable()) return button;
            }
            return null;
        }

        private void SelectHovered(Vector2 position)
        {
            if (EventSystem.current == null) return;
            var button = HoveredButton(position);
            // Background motion must not erase the initial keyboard/gamepad selection.
            if (button == null) return;
            GameObject target = button.gameObject;
            if (EventSystem.current.currentSelectedGameObject != target) EventSystem.current.SetSelectedGameObject(target);
        }

        private void SelectFirst()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }

        private void OnDisable()
        {
            StopAllCoroutines(); ready = false;
            if (navigate != null) navigate.performed -= OnNavigate;
            navigate = null;
            if (inputModule != null) inputModule.enabled = false;
        }
    }
}
