using Levels.Audio;
using System;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField] private ParticleSystem deathSmoke;

        private EnemyAI enemyAI;
        private EnemyProgression progression;

        public int CurrentHealth { get; private set; }
        public int BaseMaxHealth => maxHealth;
        public int MaxHealth { get; private set; }
        public event Action<int, int> HealthChanged;
        public event Action<int> DamageApplied;
        public event Action Defeated;
        public Func<int, int> DamageLimit { private get; set; }
        // MaxHealth is zero only before Awake, e.g. a disabled placed prefab instance.
        public bool IsAlive => MaxHealth == 0 || CurrentHealth > 0;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            MaxHealth = maxHealth;
            enemyAI = GetComponent<EnemyAI>();
            progression = GetComponent<EnemyProgression>();
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetInitialMaxHealth(int maximum)
        {
            int lostHealth = MaxHealth - CurrentHealth;
            MaxHealth = Mathf.Max(1, maximum);
            if (CurrentHealth > 0) CurrentHealth = Mathf.Max(1, MaxHealth - lostHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public bool TakeDamage(int amount)
        {
            return TakeDamage(amount, Vector2.zero);
        }

        public bool TakeDamage(int amount, Vector2 knockbackDirection)
        {
            if (!Levels.Core.GameFlow.IsGameplayActive || amount <= 0 || CurrentHealth == 0) return false;
            int appliedDamage = Mathf.Min(DamageLimit?.Invoke(amount) ?? amount, CurrentHealth);
            if (appliedDamage <= 0) return false;
            CurrentHealth -= appliedDamage;
            if (progression != null)
            {
                bool cyclops = progression.Type == Levels.Core.EnemyType.Cyclops;
                Cue sound = CurrentHealth == 0
                    ? (cyclops ? Cue.CyclopsDeath : Cue.GoblinDeath)
                    : (cyclops ? Cue.CyclopsPain : Cue.GoblinPain);
                GameAudio.PlayAt(sound, transform.position);
            }
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            DamageApplied?.Invoke(appliedDamage);
            if (CurrentHealth == 0)
            {
                GetComponent<EnemyReward>()?.GrantOnce();
                if (deathSmoke != null)
                {
                    deathSmoke.transform.SetParent(null, true);
                    deathSmoke.Play();
                }
                Defeated?.Invoke();
                Destroy(gameObject);
            }
            else if (enemyAI != null)
                enemyAI.ApplyKnockback(knockbackDirection);
            return true;
        }
    }
}
