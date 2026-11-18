using Levels.Core;
using Levels.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private UnityEngine.UI.Text healthText;
        [SerializeField] private UnityEngine.UI.Text experienceText;
        [SerializeField] private UnityEngine.UI.Text goldText;
        [SerializeField] private UnityEngine.UI.Text levelText;
        [SerializeField] private Text shieldText;
        [SerializeField] private GameObject potionDisplay;
        [SerializeField] private Text potionCountText;
        [SerializeField] private Text potionHintText;
        [SerializeField] private CanvasGroup potionStyle;
        [SerializeField] private Color ringHealthColor = new(1f, .28f, .28f);

        private GameSession session;
        private Color normalHealthColor;

        private void Awake()
        {
            if (healthText != null) normalHealthColor = healthText.color;
        }

        private void OnEnable()
        {
            session = GameSession.Instance;
            if (session == null || playerHealth == null || healthText == null ||
                experienceText == null || goldText == null)
            {
                Debug.LogError("PlayerHealthHUD requires a GameSession, PlayerHealth and three Text references.", this);
                return;
            }

            session.Changed += UpdateText;
            UpdateText();
        }

        private void OnDisable()
        {
            if (session != null) session.Changed -= UpdateText;
            session = null;
        }

        private void UpdateText()
        {
            healthText.text = $"HP: {session.PlayerHealth}/{session.PlayerMaxHealth}";
            healthText.color = session.HasPowerRing ? ringHealthColor : normalHealthColor;
            if (shieldText != null)
            {
                shieldText.gameObject.SetActive(session.ShieldCharges > 0);
                shieldText.text = $"Schild {session.ShieldCharges}/{GameSession.MaxShieldCharges}";
            }
            if (potionDisplay != null)
            {
                potionCountText.text = $"Health Potion ×{session.PotionCount}";
                potionHintText.text = session.HasPowerRing ? "Q / X · Durch Ring gesperrt" : "Q / X · Benutzen";
                potionStyle.alpha = session.HasPowerRing ? .65f : 1f;
                UpdatePotionVisibility();
            }
            experienceText.text = $"EXP: {session.CurrentExperience}";
            goldText.text = $"Gold: {session.CurrentGold}";
            if (levelText != null)
                levelText.text = $"Player Lv {session.PlayerLevel}  |  Goblin Lv {session.GoblinLevel}  |  Cyclops Lv {session.CyclopsLevel}";
        }

        private void LateUpdate() => UpdatePotionVisibility();

        private void UpdatePotionVisibility()
        {
            if (potionDisplay == null || session == null) return;
            bool visible = GameFlow.IsGameplayActive && session.PotionCount > 0;
            if (potionDisplay.activeSelf != visible) potionDisplay.SetActive(visible);
        }
    }
}
