using Levels.Audio;
using Levels.Core;
using Levels.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [SerializeField] private Image criticalPortraitOverlay;
        [SerializeField, Min(.01f)] private float warningFadeIn = .1f;
        [SerializeField, Min(.01f)] private float warningFadeOut = .8f;
        [SerializeField, Range(0f, 1f)] private float warningOpacity = .65f;
        private const double WarningPulseInterval = 2d;

        private GameSession session;
        private bool criticalHealth;
        private bool warningVisible;
        private double nextPulseAt;
        private double pulseStartedAt;

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
            SetCriticalHealth(false);
        }

        private void LateUpdate()
        {
            if (!criticalHealth) return;
            if (!GameFlow.IsGameplayActive || Time.timeScale <= 0f)
            {
                HideWarning();
                return;
            }

            double now = Time.timeAsDouble;
            if (now >= nextPulseAt && GameAudio.TryPlayCriticalHealthWarning())
            {
                pulseStartedAt = now;
                nextPulseAt = (now - nextPulseAt > Time.deltaTime ? now : nextPulseAt) + WarningPulseInterval;
                warningVisible = true;
            }

            if (!warningVisible) return;
            float fadeIn = Mathf.Max(.01f, warningFadeIn);
            float fadeOut = Mathf.Max(.01f, warningFadeOut);
            float pulseTime = (float)(now - pulseStartedAt) + Mathf.Min(Time.deltaTime, fadeIn);
            float opacity = pulseTime < fadeIn
                ? pulseTime / fadeIn
                : 1f - Mathf.Clamp01((pulseTime - fadeIn) / fadeOut);
            SetPortraitOpacity(opacity * warningOpacity);

        }

        private void SetCriticalHealth(bool active)
        {
            if (criticalHealth == active) return;
            criticalHealth = active;
            nextPulseAt = 0d;
            if (!active) HideWarning();
        }

        private void HideWarning()
        {
            if (!warningVisible) return;
            warningVisible = false;
            SetPortraitOpacity(0f);
            GameAudio.StopCriticalHealthWarning();
        }

        private void SetPortraitOpacity(float opacity)
        {
            if (criticalPortraitOverlay == null) return;
            Color color = criticalPortraitOverlay.color;
            if (color.a == opacity) return;
            color.a = opacity;
            criticalPortraitOverlay.color = color;
        }

        private void Refresh()
        {
            // Read one session snapshot so both numbers and the fill reflect the same health update.
            int health = session.PlayerHealth;
            int maximum = session.PlayerMaxHealth > 0 ? session.PlayerMaxHealth : playerHealth.MaxHealth;
            SetCriticalHealth(health > 0 && (long)health * 5 <= maximum);
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
