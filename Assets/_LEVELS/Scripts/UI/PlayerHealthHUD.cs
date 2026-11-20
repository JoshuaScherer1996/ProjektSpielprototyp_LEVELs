using Levels.Core;
using Levels.Player;
using TMPro;
using UnityEngine;

namespace Levels.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private GameObject shieldBadge;
        [SerializeField] private TMP_Text shieldText;
        [SerializeField] private TMP_Text potionCountText;
        [SerializeField] private TMP_Text potionHintText;
        [SerializeField] private CanvasGroup potionIcon;

        private GameSession session;

        private void OnEnable()
        {
            session = GameSession.Instance;
            if (session == null || playerHealth == null || healthFill == null || healthText == null ||
                levelText == null || experienceText == null || goldText == null || shieldBadge == null || shieldText == null ||
                potionCountText == null || potionHintText == null || potionIcon == null)
            {
                Debug.LogError("PlayerHealthHUD requires a GameSession, PlayerHealth and all HUD references.", this);
                session = null;
                return;
            }

            session.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (session != null) session.Changed -= Refresh;
            session = null;
        }

        private void Refresh()
        {
            // Read one session snapshot so both numbers and the fill reflect the same health update.
            int health = session.PlayerHealth;
            int maximum = session.PlayerMaxHealth > 0 ? session.PlayerMaxHealth : playerHealth.MaxHealth;
            healthFill.anchorMax = new Vector2((float)health / maximum, 1f);
            healthText.text = $"HP {health}/{maximum}";
            levelText.text = $"LV {session.PlayerLevel}";
            experienceText.text = $"EXP {session.CurrentExperience}";
            goldText.text = session.CurrentGold.ToString();

            int shieldCharges = session.ShieldCharges;
            shieldBadge.SetActive(shieldCharges > 0);
            shieldText.text = $"Schild {shieldCharges}/{GameSession.MaxShieldCharges}";

            int potions = session.PotionCount;
            potionCountText.text = $"×{potions}";
            potionIcon.alpha = potions > 0 && !session.HasPowerRing ? 1f : .5f;
            potionHintText.text = session.HasPowerRing ? "Q / X · Gesperrt" : "Q / X · Benutzen";
        }
    }
}
