using System.Collections;
using System.Collections.Generic;
using Levels.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
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

        private readonly HashSet<EnemyHealth> hitThisAttack = new HashSet<EnemyHealth>();
        private PlayerInput playerInput;
        private PlayerInputReader inputReader;
        private InputAction attackAction;
        private Vector2 facing = Vector2.down;
        private float nextAttackTime;
        private Coroutine slashRoutine;
        private int progressionDamage;

        public Vector2 Facing => facing;
        public int BaseDamage => damage;
        public int Damage => progressionDamage > 0 ? progressionDamage : damage;
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
        }

        private void OnDisable()
        {
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
        }

        private void UpdateFacing()
        {
            Vector2 direction = Vector2.zero;
            bool mouseControl = playerInput.currentControlScheme == "Keyboard&Mouse";
            if (mouseControl && Mouse.current != null && Camera.main != null)
            {
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                if (Camera.main.pixelRect.Contains(mousePosition))
                    direction = (Vector2)Camera.main.ScreenToWorldPoint(mousePosition) - (Vector2)transform.position;
            }
            else if (Gamepad.current != null)
                direction = Gamepad.current.rightStick.ReadValue();

            if (direction.sqrMagnitude < 0.04f && !mouseControl)
                direction = inputReader.MoveInput;
            if (direction.sqrMagnitude < 0.04f) return;

            facing = direction.normalized;
            if (aimVisual != null)
                aimVisual.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f);
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            if (context.control == Mouse.current?.leftButton &&
                LockedBossDoorInteraction.TryHandlePointer(GetComponent<PlayerMovement>())) return;
            TryAttack();
        }

        public bool TryAttack()
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || !isActiveAndEnabled || !playerInput.enabled || hitbox == null || Time.time < nextAttackTime)
                return false;

            UpdateFacing();
            nextAttackTime = Time.time + cooldownSeconds;
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
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, size, hitbox.transform.eulerAngles.z);

            hitThisAttack.Clear();
            foreach (Collider2D overlap in overlaps)
            {
                EnemyHealth enemy = overlap.GetComponentInParent<EnemyHealth>();
                if (enemy != null && hitThisAttack.Add(enemy))
                {
                    Vector2 knockback = (Vector2)(enemy.transform.position - transform.position);
                    enemy.TakeDamage(Damage, knockback.sqrMagnitude > 0.001f ? knockback.normalized : facing);
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
