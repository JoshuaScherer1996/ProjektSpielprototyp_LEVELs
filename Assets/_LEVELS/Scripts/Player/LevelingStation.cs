using Levels.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    public sealed class LevelingStation : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionRange = 2f;
        [SerializeField] private LevelingMenu menu;
        [SerializeField] private GameObject interactionPrompt;
        private PlayerMovement player;
        private InputAction interact;

        private void Start()
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.GetComponent<PlayerMovement>();
            interact = target.GetComponent<PlayerInput>().actions.FindAction("Player/Interact", true);
        }

        private void Update()
        {
            bool inRange = player != null &&
                (player.transform.position - transform.position).sqrMagnitude <= interactionRange * interactionRange;
            if (interactionPrompt != null) interactionPrompt.SetActive(inRange && !menu.IsOpen);
            if (!inRange || interact == null || !interact.WasPerformedThisFrame()) return;
            if (menu.IsOpen) menu.Close();
            else menu.Open(player);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
