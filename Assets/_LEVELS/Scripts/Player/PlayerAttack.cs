using Levels.Audio;
using System.Collections;
using System.Collections.Generic;
using Levels.Core;
using Levels.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

namespace Levels.Player
{
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(PlayerInputReader))]
    public sealed class PlayerAttack : MonoBehaviour
    {
        [SerializeField] private string attackActionPath = "Player/Attack";
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.3f;
        [SerializeField, Min(0f)] private float slashVisibleSeconds = 0.18f;
        [SerializeField] private Transform aimVisual;
        [SerializeField] private SpriteRenderer slashRenderer;
        [SerializeField] private BoxCollider2D hitbox;

        private const string GamepadScheme = "Gamepad";
        private const string MouseScheme = "Keyboard&Mouse";
        private const float RightStickDeadzoneSquared = .04f;
        private const float LeftStickDeadzoneSquared = .01f;
        private const float PointerSwitchDistanceSquared = 4f;

        private readonly HashSet<EnemyHealth> hitThisAttack = new HashSet<EnemyHealth>();
        private PlayerInput playerInput;
        private PlayerInputReader inputReader;
        private InputAction attackAction;
        private Vector2 facing = Vector2.down;
        private Vector2 swingFacing = Vector2.down;
        private float nextAttackTime;
        private Coroutine slashRoutine;
        private int progressionDamage;
        private bool filtersDeviceSwitching;
        private Mouse inactiveMouse;
        private Vector2 inactivePointerPosition;

        public Vector2 Facing => slashRoutine != null ? swingFacing : facing;
        public float FacingAngle => AngleFor(Facing);
        public int BaseDamage => damage;
        public int Damage
        {
            get
            {
                int regularDamage = progressionDamage > 0 ? progressionDamage : damage;
                return GameSession.Instance != null && GameSession.Instance.HasPowerRing
                    ? (int)System.Math.Min(int.MaxValue, regularDamage * 10L)
                    : regularDamage;
            }
        }
        public void SetProgressionDamage(int value) => progressionDamage = Mathf.Max(1, value);

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            inputReader = GetComponent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            attackAction = playerInput.actions.FindAction(attackActionPath, true);
            attackAction.performed += OnAttack;

            filtersDeviceSwitching = !playerInput.neverAutoSwitchControlSchemes;
            if (filtersDeviceSwitching)
            {
                playerInput.neverAutoSwitchControlSchemes = true;
                InputUser.listenForUnpairedDeviceActivity++;
                InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;
            }
        }

        private void OnDisable()
        {
            if (filtersDeviceSwitching)
            {
                InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
                InputUser.listenForUnpairedDeviceActivity--;
                playerInput.neverAutoSwitchControlSchemes = false;
                filtersDeviceSwitching = false;
                inactiveMouse = null;
            }
            if (attackAction != null)
            {
                attackAction.performed -= OnAttack;
                attackAction = null;
            }
            if (slashRoutine != null)
            {
                StopCoroutine(slashRoutine);
                slashRoutine = null;
            }
            if (slashRenderer != null) slashRenderer.enabled = false;
        }

        private void Update()
        {
            UpdateFacing();
            RotateAim(Facing);
        }

        private void UpdateFacing(InputDevice attackDevice = null)
        {
            bool gamepadControl = attackDevice is Gamepad ||
                (attackDevice == null && playerInput.currentControlScheme == GamepadScheme);
            if (gamepadControl)
            {
                var gamepad = attackDevice as Gamepad ?? playerInput.GetDevice<Gamepad>();
                Vector2 aim = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
                if (aim.sqrMagnitude < RightStickDeadzoneSquared) aim = inputReader.MoveInput;
                if (aim != Vector2.zero) facing = aim.normalized;
                return;
            }

            bool mouseControl = attackDevice is Mouse || attackDevice is Keyboard ||
                (attackDevice == null && playerInput.currentControlScheme == MouseScheme);
            if (!mouseControl) return;

            var mouse = attackDevice as Mouse ?? playerInput.GetDevice<Mouse>();
            var camera = Camera.main;
            if (mouse == null || camera == null) return;

            var pointer = mouse.position.ReadValue();
            if (!camera.pixelRect.Contains(pointer)) return;

            Vector2 direction = (Vector2)camera.ScreenToWorldPoint(pointer) - (Vector2)transform.position;
            if (direction.sqrMagnitude >= .04f) facing = direction.normalized;
        }

        private void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
        {
            if (!playerInput.isActiveAndEnabled || PlayerInput.all.Count != 1) return;

            bool pressed = control is ButtonControl button &&
                button.ReadValueFromEvent(eventPtr, out float value) && value >= button.pressPointOrDefault;
            switch (control.device)
            {
                case Gamepad gamepad when pressed ||
                    StickIsActive(gamepad.leftStick, eventPtr, LeftStickDeadzoneSquared) ||
                    StickIsActive(gamepad.rightStick, eventPtr, RightStickDeadzoneSquared):
                    inactiveMouse = null;
                    playerInput.SwitchCurrentControlScheme(GamepadScheme, gamepad);
                    break;
                case Keyboard keyboard when pressed:
                    var pairedMouse = playerInput.GetDevice<Mouse>() ?? Mouse.current;
                    if (pairedMouse != null) SwitchToMouseScheme(keyboard, pairedMouse);
                    break;
                case Mouse mouse when pressed || PointerMoved(mouse, eventPtr):
                    var pairedKeyboard = playerInput.GetDevice<Keyboard>() ?? Keyboard.current;
                    if (pairedKeyboard != null) SwitchToMouseScheme(pairedKeyboard, mouse);
                    break;
            }
        }

        private bool PointerMoved(Mouse mouse, InputEventPtr eventPtr)
        {
            if (!mouse.position.ReadValueFromEvent(eventPtr, out Vector2 pointer)) return false;
            if (inactiveMouse != mouse)
            {
                inactiveMouse = mouse;
                inactivePointerPosition = mouse.position.ReadValue();
            }
            return (pointer - inactivePointerPosition).sqrMagnitude >= PointerSwitchDistanceSquared;
        }

        private void SwitchToMouseScheme(Keyboard keyboard, Mouse mouse)
        {
            inactiveMouse = null;
            playerInput.SwitchCurrentControlScheme(MouseScheme, keyboard, mouse);
        }

        private static bool StickIsActive(StickControl stick, InputEventPtr eventPtr, float deadzoneSquared) =>
            stick.ReadValueFromEvent(eventPtr, out Vector2 value) && value.sqrMagnitude >= deadzoneSquared;

        private void RotateAim(Vector2 direction)
        {
            if (aimVisual != null)
                aimVisual.localRotation = Quaternion.Euler(0f, 0f, AngleFor(direction));
        }

        private static float AngleFor(Vector2 direction) =>
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        private void OnAttack(InputAction.CallbackContext context)
        {
            var device = context.control.device;
            if (device is Mouse mouse && context.control == mouse.leftButton &&
                LockedBossDoorInteraction.TryHandlePointer(GetComponent<PlayerMovement>())) return;
            PerformAttack(device);
        }

        public bool TryAttack() => PerformAttack(null);

        private bool PerformAttack(InputDevice device)
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || !isActiveAndEnabled || !playerInput.enabled || hitbox == null || Time.time < nextAttackTime)
                return false;

            UpdateFacing(device);
            swingFacing = facing;
            RotateAim(swingFacing);
            nextAttackTime = Time.time + cooldownSeconds;
            GameAudio.Play(Cue.Sword);
            if (slashRenderer != null)
            {
                if (slashRoutine != null) StopCoroutine(slashRoutine);
                slashRenderer.enabled = true;
                slashRoutine = StartCoroutine(HideSlash());
            }

            Vector3 scale = hitbox.transform.lossyScale;
            Vector2 size = new Vector2(hitbox.size.x * Mathf.Abs(scale.x),
                hitbox.size.y * Mathf.Abs(scale.y));
            Vector2 center = hitbox.transform.TransformPoint(hitbox.offset);
            Physics2D.SyncTransforms();
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, size, hitbox.transform.eulerAngles.z);

            hitThisAttack.Clear();
            foreach (Collider2D overlap in overlaps)
            {
                EnemyHealth enemy = overlap.GetComponentInParent<EnemyHealth>();
                if (enemy != null && hitThisAttack.Add(enemy))
                {
                    Vector2 knockback = (Vector2)(enemy.transform.position - transform.position);
                    enemy.TakeDamage(Damage, knockback.sqrMagnitude > 0.001f ? knockback.normalized : swingFacing);
                }
            }
            return true;
        }

        private IEnumerator HideSlash()
        {
            yield return new WaitForSeconds(slashVisibleSeconds);
            if (slashRenderer != null) slashRenderer.enabled = false;
            slashRoutine = null;
        }
    }
}
