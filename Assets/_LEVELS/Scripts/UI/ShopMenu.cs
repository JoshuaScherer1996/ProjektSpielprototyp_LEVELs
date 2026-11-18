using Levels.Core;
using Levels.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
        [SerializeField] private Button shieldButton;
        [SerializeField] private Button ringButton;
        [SerializeField] private Button potionButton;
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
        private (ShopOffer Offer, Button Button, Text Label, string Name, string Effect)[] offers;
        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            panel.SetActive(false);
            offers = new[]
            {
                (ShopOffer.Healing, healingButton, healingButton.GetComponentInChildren<Text>(true), "Heilung", "Stellt LP vollständig wieder her."),
                (ShopOffer.MaxHealth, maxHealthButton, maxHealthButton.GetComponentInChildren<Text>(true), "Health+", "+1 Max. LP für diesen Durchlauf."),
                (ShopOffer.Shield, shieldButton, shieldButton.GetComponentInChildren<Text>(true), "Magisches Schild", "Blockiert die nächsten 3 gültigen Treffer."),
                (ShopOffer.PowerRing, ringButton, ringButton.GetComponentInChildren<Text>(true), "Ring der Macht", "×10 Angriffsschaden, dauerhaft 1/1 LP."),
                (ShopOffer.Potion, potionButton, potionButton.GetComponentInChildren<Text>(true), "Health Potion", "Heilt bei Benutzung 20 % der max. LP.")
            };
        }

        private void OnEnable()
        {
            session = GameSession.Instance;
            session.Changed += Refresh;
            healingButton.onClick.AddListener(BuyHealing);
            maxHealthButton.onClick.AddListener(BuyMaxHealth);
            shieldButton.onClick.AddListener(BuyShield);
            ringButton.onClick.AddListener(BuyRing);
            potionButton.onClick.AddListener(BuyPotion);
            closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            Close();
            if (session != null) session.Changed -= Refresh;
            healingButton.onClick.RemoveListener(BuyHealing);
            maxHealthButton.onClick.RemoveListener(BuyMaxHealth);
            shieldButton.onClick.RemoveListener(BuyShield);
            ringButton.onClick.RemoveListener(BuyRing);
            potionButton.onClick.RemoveListener(BuyPotion);
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
            feedback.text = "";
            Refresh();
            SelectFirstAvailable();
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
                SelectFirstAvailable();
        }

        private void SelectFirstAvailable()
        {
            if (EventSystem.current == null) return;
            foreach (var offer in offers)
            {
                if (!offer.Button.interactable) continue;
                EventSystem.current.SetSelectedGameObject(offer.Button.gameObject);
                return;
            }
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        private void ClearSelectionOnMouseMove()
        {
            if (Mouse.current == null || Mouse.current.delta.ReadValue().sqrMagnitude < 0.01f) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void BuyHealing() => Buy(ShopOffer.Healing);
        public void BuyMaxHealth() => Buy(ShopOffer.MaxHealth);
        public void BuyShield() => Buy(ShopOffer.Shield);
        public void BuyRing() => Buy(ShopOffer.PowerRing);
        public void BuyPotion() => Buy(ShopOffer.Potion);

        private void Buy(ShopOffer offer)
        {
            if (!GameFlow.IsGameplayActive || !IsOpen || Time.frameCount <= openedFrame || Time.frameCount == lastPurchaseFrame) return;
            lastPurchaseFrame = Time.frameCount;
            ShopPurchaseResult result = transactions.TryPurchase(offer, health);
            feedback.text = result == ShopPurchaseResult.Success ? offer switch
            {
                ShopOffer.Healing => "LP vollständig wiederhergestellt.",
                ShopOffer.MaxHealth => "+1 Max. LP gekauft.",
                ShopOffer.Shield => "Magisches Schild aktiviert.",
                ShopOffer.PowerRing => "Ring der Macht aktiviert.",
                ShopOffer.Potion => $"Health Potion gekauft. Bestand: {session.PotionCount}",
                _ => ""
            } : StatusText(result);
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.TryGetComponent(out Button selected) && !selected.interactable)
                SelectFirstAvailable();
        }

        private void Refresh()
        {
            if (!IsOpen || transactions == null) return;
            details.text = $"Gold: {session.CurrentGold}     HP: {session.PlayerHealth}/{session.PlayerMaxHealth}";
            foreach (var entry in offers)
            {
                long price = transactions.GetPrice(entry.Offer, session);
                ShopPurchaseResult status = session.GetOfferStatus(entry.Offer, price);
                entry.Button.interactable = status == ShopPurchaseResult.Success;
                string availability = entry.Offer == ShopOffer.Shield && status == ShopPurchaseResult.AlreadyActive
                    ? $"Aktiv ({session.ShieldCharges}/{GameSession.MaxShieldCharges})" : StatusText(status);
                entry.Label.text = $"{entry.Name} · {price} Gold · {availability}\n{entry.Effect}";
            }
        }

        private static string StatusText(ShopPurchaseResult status) => status switch
        {
            ShopPurchaseResult.Success => "Kaufbar",
            ShopPurchaseResult.FullHealth => "LP voll",
            ShopPurchaseResult.NotEnoughGold => "Zu wenig Gold",
            ShopPurchaseResult.LimitReached => "Limit erreicht",
            ShopPurchaseResult.AlreadyActive => "Bereits aktiv",
            ShopPurchaseResult.BlockedByRing => "Durch Ring gesperrt",
            _ => "Nicht verfügbar"
        };
    }
}
