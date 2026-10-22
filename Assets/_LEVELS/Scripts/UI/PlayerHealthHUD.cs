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

        private void OnEnable()
        {
            if (playerHealth == null || healthText == null)
            {
                Debug.LogError("PlayerHealthHUD requires a PlayerHealth and a Text reference.", this);
                return;
            }

            playerHealth.HealthChanged += UpdateText;
            UpdateText(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.HealthChanged -= UpdateText;
        }

        private void Start()
        {
            if (playerHealth != null && healthText != null)
                UpdateText(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void UpdateText(int current, int maximum)
        {
            healthText.text = $"HP: {current}/{maximum}";
        }
    }
}
