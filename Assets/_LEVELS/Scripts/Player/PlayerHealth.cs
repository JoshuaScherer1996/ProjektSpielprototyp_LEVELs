using System;
using Levels.Core;
using UnityEngine;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;

        public int CurrentHealth { get; private set; }
        public int BaseMaxHealth => maxHealth;
        public int MaxHealth => GameSession.Instance != null && GameSession.Instance.PlayerMaxHealth > 0
            ? GameSession.Instance.PlayerMaxHealth : maxHealth;
        public event Action<int, int> HealthChanged;

        private void Awake()
        {
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogError("PlayerHealth requires an active GameSession.", this);
                CurrentHealth = maxHealth;
                return;
            }

            session.InitializePlayerHealth(maxHealth);
            CurrentHealth = session.PlayerHealth;
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || CurrentHealth == 0) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            GameSession.Instance?.UpdatePlayerHealth(CurrentHealth, MaxHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth == 0)
                Destroy(gameObject);
            return true;
        }

        public void SetMaxHealth(int maximum)
        {
            maximum = Mathf.Max(1, maximum);
            if (MaxHealth == maximum) return;
            // Keep absolute current HP: buying maximum HP does not heal.
            CurrentHealth = Mathf.Min(CurrentHealth, maximum);
            GameSession.Instance.UpdatePlayerHealth(CurrentHealth, maximum);
            HealthChanged?.Invoke(CurrentHealth, maximum);
        }
    }
}
