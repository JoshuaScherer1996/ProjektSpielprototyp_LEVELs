using Levels.Core;
using Levels.Player;
using UnityEngine;

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

        private GameSession session;

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
            experienceText.text = $"EXP: {session.CurrentExperience}";
            goldText.text = $"Gold: {session.CurrentGold}";
        }
    }
}
