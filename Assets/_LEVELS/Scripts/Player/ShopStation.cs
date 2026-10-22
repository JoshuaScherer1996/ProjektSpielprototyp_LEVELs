using Levels.Core;
using Levels.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D), typeof(ShopTransactions))]
    public sealed class ShopStation : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float interactionRange = 2f;
        [SerializeField] private ShopMenu menu;
        [SerializeField] private GameObject interactionPrompt;
        private PlayerMovement player;
        private InputAction interact;
        private ShopTransactions transactions;

        private void Start()
        {
            transactions = GetComponent<ShopTransactions>();
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.GetComponent<PlayerMovement>();
            interact = target.GetComponent<PlayerInput>().actions.FindAction("Player/Interact", true);
        }

        private void Update()
        {
            bool inRange = player != null && menu != null &&
                (player.transform.position - transform.position).sqrMagnitude <= interactionRange * interactionRange;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(inRange && player.enabled && !menu.IsOpen);
            if (!inRange || interact == null || !interact.WasPerformedThisFrame()) return;
            if (menu.IsOpen) menu.Close();
            else menu.Open(player, transactions);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
