using System;
using UnityEngine;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public event Action<int, int> HealthChanged;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || CurrentHealth == 0) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (CurrentHealth == 0)
                Destroy(gameObject);
            return true;
        }
    }
}
