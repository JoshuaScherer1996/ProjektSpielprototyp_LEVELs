using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField] private ParticleSystem deathSmoke;

        private EnemyAI enemyAI;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            enemyAI = GetComponent<EnemyAI>();
        }

        public bool TakeDamage(int amount)
        {
            return TakeDamage(amount, Vector2.zero);
        }

        public bool TakeDamage(int amount, Vector2 knockbackDirection)
        {
            if (amount <= 0 || CurrentHealth == 0) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth == 0)
            {
                GetComponent<EnemyReward>()?.GrantOnce();
                if (deathSmoke != null)
                {
                    deathSmoke.transform.SetParent(null, true);
                    deathSmoke.Play();
                }
                Destroy(gameObject);
            }
            else if (enemyAI != null)
                enemyAI.ApplyKnockback(knockbackDirection);
            return true;
        }
    }
}
