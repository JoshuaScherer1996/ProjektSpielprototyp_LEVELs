using Levels.Core;
using Levels.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    public sealed class ShopMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text details;
        [SerializeField] private UnityEngine.UI.Text feedback;
        [SerializeField] private UnityEngine.UI.Button healingButton;
        [SerializeField] private UnityEngine.UI.Button maxHealthButton;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [SerializeField] private InputSystemUIInputModule inputModule;

        private GameSession session;
        private PlayerMovement movement;
        private PlayerAttack attack;
        private PlayerHealth health;
        private ShopTransactions transactions;
        private bool restoreAttack;
        private int openedFrame;
        private int lastPurchaseFrame = -1;
        private InputAction cancelAction;
        private InputAction moveAction;
        private bool cancelRequested;
        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake() => panel.SetActive(false);

        private void OnEnable()
        {
            session = GameSession.Instance;
            session.Changed += Refresh;
            healingButton.onClick.AddListener(BuyHealing);
            maxHealthButton.onClick.AddListener(BuyMaxHealth);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            Close();
            if (session != null) session.Changed -= Refresh;
            healingButton.onClick.RemoveListener(BuyHealing);
            maxHealthButton.onClick.RemoveListener(BuyMaxHealth);
            closeButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount <= openedFrame) return;
            if (movement == null || health == null || health.CurrentHealth <= 0 || cancelRequested)
            {
                cancelRequested = false;
                Close();
                return;
            }

            ClearSelectionOnMouseMove();
        }

        public bool Open(PlayerMovement player, ShopTransactions shop)
        {
            // The disabled movement component is the same exclusive control lock used by leveling/loading.
            if (!GameFlow.IsGameplayActive || IsOpen || player == null || !player.enabled || shop == null || inputModule == null) return false;
            health = player.GetComponent<PlayerHealth>();
            if (health == null || health.CurrentHealth <= 0) return false;
            movement = player;
            transactions = shop;
            attack = player.GetComponent<PlayerAttack>();
            restoreAttack = attack != null && attack.enabled;
            movement.enabled = false;
            if (attack != null) attack.enabled = false;
            openedFrame = Time.frameCount;
            panel.SetActive(true);
            inputModule.enabled = true;
            cancelAction = inputModule.cancel.action;
            if (cancelAction != null) cancelAction.performed += OnCancel;
            moveAction = inputModule.move.action;
            if (moveAction != null) moveAction.performed += OnMove;
            cancelRequested = false;
            feedback.text = "Choose an offer. Nothing is spent until you buy.";
            Refresh();
            EventSystem.current.SetSelectedGameObject(healingButton.gameObject);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            // A/ Cross is both UI Submit and Interact: do not reopen the station with the closing press.
            PlayerInputReader.ConsumeInteractForCurrentFrame();
            if (cancelAction != null) cancelAction.performed -= OnCancel;
            if (moveAction != null) moveAction.performed -= OnMove;
            cancelAction = null;
            moveAction = null;
            panel.SetActive(false);
            // Unity may destroy the shared EventSystem before this menu during Play-mode teardown.
            if (inputModule != null) inputModule.enabled = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            // Keep PlayerInput and device pairing alive: held stick input works immediately after closing.
            if (movement != null) movement.enabled = GameFlow.IsGameplayActive;
            if (attack != null) attack.enabled = restoreAttack && GameFlow.IsGameplayActive;
            movement = null;
            attack = null;
            health = null;
            transactions = null;
        }

        private void OnCancel(InputAction.CallbackContext context) => cancelRequested = true;

        private void OnMove(InputAction.CallbackContext context)
        {
            if (context.ReadValue<Vector2>().sqrMagnitude > 0.01f && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(healingButton.gameObject);
        }

        private void ClearSelectionOnMouseMove()
        {
            if (Mouse.current == null || Mouse.current.delta.ReadValue().sqrMagnitude < 0.01f) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void BuyHealing() => Buy(ShopOffer.Healing);
        public void BuyMaxHealth() => Buy(ShopOffer.MaxHealth);

        private void Buy(ShopOffer offer)
        {
            if (!GameFlow.IsGameplayActive || !IsOpen || Time.frameCount <= openedFrame || Time.frameCount == lastPurchaseFrame) return;
            lastPurchaseFrame = Time.frameCount;
            ShopPurchaseResult result = transactions.TryPurchase(offer, health);
            switch (result)
            {
                case ShopPurchaseResult.Success:
                    feedback.text = offer == ShopOffer.Healing ? "HP fully restored." : "+1 maximum HP. Current HP unchanged.";
                    break;
                case ShopPurchaseResult.FullHealth: feedback.text = "HP are already full. No gold spent."; break;
                case ShopPurchaseResult.NotEnoughGold: feedback.text = "Not enough gold. No purchase made."; break;
                case ShopPurchaseResult.LimitReached: feedback.text = "Maximum HP limit reached. No gold spent."; break;
                default: feedback.text = "Purchase unavailable. No gold spent."; break;
            }
        }

        private void Refresh()
        {
            if (!IsOpen || transactions == null) return;
            details.text = $"Gold: {session.CurrentGold}     HP: {session.PlayerHealth}/{session.PlayerMaxHealth}\n\n" +
                $"Healing - {transactions.HealingPrice} gold\nRestore HP to maximum (no overheal).\n\n" +
                $"Max HP +1 - {transactions.MaxHealthPrice} gold\nLasts this session; current HP unchanged.";
        }
    }
}
