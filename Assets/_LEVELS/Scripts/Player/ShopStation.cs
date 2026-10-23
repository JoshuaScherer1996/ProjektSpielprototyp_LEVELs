using Levels.Core;
using Levels.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D), typeof(ShopTransactions))]
    public sealed class ShopStation : MonoBehaviour, IStationInteractionTarget
    {
        [SerializeField, Min(.1f)] private float interactionRange = 2f;
        [SerializeField] private ShopMenu menu;
        [SerializeField] private GameObject interactionPrompt;
        private PlayerMovement player;
        private InputAction interact;
        private ShopTransactions transactions;

        float IStationInteractionTarget.InteractionRange => interactionRange;
        int IStationInteractionTarget.SelectionPriority => 1;
        bool IStationInteractionTarget.IsAvailable => isActiveAndEnabled && player != null && menu != null && transactions != null && !menu.IsOpen;

        private void Awake()
        {
            transactions = GetComponent<ShopTransactions>();
        }

        private void OnEnable()
        {
            StationInteractionSelector.Register(this);
        }

        private void OnDisable()
        {
            StationInteractionSelector.Unregister(this);
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }

        private void Start()
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.GetComponent<PlayerMovement>();
            PlayerInput playerInput = target.GetComponent<PlayerInput>();
            if (playerInput != null)
                interact = playerInput.actions.FindAction("Player/Interact", true);
        }

        private void Update()
        {
            IStationInteractionTarget selected = StationInteractionSelector.GetBestTarget(player);
            bool isSelected = ReferenceEquals(selected, this);
            if (interactionPrompt != null) interactionPrompt.SetActive(isSelected);
            if (isSelected) ((IStationInteractionTarget)this).TryOpen();
        }

        float IStationInteractionTarget.DistanceFrom(Vector2 position)
        {
            return Vector2.Distance(position, transform.position);
        }

        float IStationInteractionTarget.SelectionDistanceFrom(Vector2 position) =>
            Vector2.Distance(position, transform.position);

        void IStationInteractionTarget.TryOpen()
        {
            if (menu != null && player != null && !menu.IsOpen &&
                PlayerInputReader.TryConsumeInteractPress(interact))
                menu.Open(player, transactions);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
