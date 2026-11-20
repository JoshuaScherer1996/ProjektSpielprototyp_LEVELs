using Levels.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Levels.Visuals
{
    [DisallowMultipleComponent]
    public sealed class DirectionalCharacterVisual : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Animator animator;
        [SerializeField] private SortingGroup sortingGroup;
        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private SpriteRenderer attackArc;
        [SerializeField] private SpriteRenderer sword;
        [SerializeField, Min(0.01f)] private float walkReferenceSpeed = 5f;

        private static readonly int[] Idle = StateHashes("Idle");
        private static readonly int[] Walk = StateHashes("Walk");
        private static readonly int[] Attack = StateHashes("Attack");
        private static readonly Vector2[] Grip =
        {
            new Vector2(.24f, .31f), new Vector2(-.24f, .32f),
            new Vector2(.24f, .31f), new Vector2(-.24f, .31f)
        };
        private static readonly float[] CardinalAngles = { 180f, 0f, -90f, 90f };

        private Vector2 previousPosition;
        private float travelSpeed;
        private int direction;
        private int activeState;
        private bool attacking;
        private float swingStarted;
        private float swordHeading;

        private void OnEnable()
        {
            previousPosition = body.position;
            travelSpeed = 0f;
            direction = 0;
            activeState = 0;
            attacking = false;
        }

        private void FixedUpdate()
        {
            Vector2 travel = body.position - previousPosition;
            previousPosition = body.position;
            float distance = travel.magnitude;
            travelSpeed = distance < 0.5f ? distance / Time.fixedDeltaTime : 0f;
            if (travelSpeed > 0.075f && (playerAttack == null || !playerAttack.isActiveAndEnabled))
                direction = CardinalDirection(travel);
        }

        private void Update()
        {
            bool walking = travelSpeed > 0.075f;
            bool aiming = playerAttack != null && playerAttack.isActiveAndEnabled;
            if (aiming)
                direction = CardinalDirection(playerAttack.Facing);
            swordHeading = aiming ? playerAttack.FacingAngle : CardinalAngles[direction];

            bool showAttack = attackArc != null && attackArc.enabled;
            if (showAttack && !attacking)
                swingStarted = Time.time;

            attacking = showAttack;
            int state = (attacking ? Attack : walking ? Walk : Idle)[direction];
            animator.speed = walking && !attacking
                ? Mathf.Clamp(travelSpeed / walkReferenceSpeed, 0.55f, 1.35f) : 1f;
            if (state == activeState)
                return;

            animator.Play(state, 0, 0f);
            activeState = state;
        }

        private void LateUpdate()
        {
            sortingGroup.sortingOrder = Mathf.RoundToInt(-body.transform.position.y * 16f);
            if (sword != null)
                PositionSword();
        }

        private void PositionSword()
        {
            sword.transform.localPosition = Grip[direction];
            sword.sortingOrder = direction == 1 ? -2 : 1;
            float angle = swordHeading;
            if (attacking)
            {
                float progress = Mathf.Clamp01((Time.time - swingStarted) /
                    Mathf.Max(.01f, animator.GetCurrentAnimatorStateInfo(0).length));
                angle += Mathf.Lerp(65f, -65f, progress);
            }
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static int CardinalDirection(Vector2 movement)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                return movement.x > 0f ? 2 : 3;
            return movement.y > 0f ? 1 : 0;
        }

        private static int[] StateHashes(string motion) => new[]
        {
            Animator.StringToHash("Base Layer." + motion + "Down"),
            Animator.StringToHash("Base Layer." + motion + "Up"),
            Animator.StringToHash("Base Layer." + motion + "Right"),
            Animator.StringToHash("Base Layer." + motion + "Left")
        };
    }
}
