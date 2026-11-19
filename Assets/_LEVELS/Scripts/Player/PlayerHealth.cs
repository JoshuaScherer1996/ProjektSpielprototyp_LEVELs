using Levels.Audio;
using System;
using Levels.Core;
using UnityEngine;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;
        [SerializeField, Min(0.02f)] private float shieldHitProtection = 0.2f;
        [SerializeField] private SpriteRenderer shieldSource;
        [SerializeField] private SpriteRenderer[] shieldOutline = Array.Empty<SpriteRenderer>();

        public int CurrentHealth { get; private set; }
        public int BaseMaxHealth => maxHealth;
        public int MaxHealth => GameSession.Instance != null && GameSession.Instance.PlayerMaxHealth > 0
            ? GameSession.Instance.PlayerMaxHealth : maxHealth;
        public event Action<int, int> HealthChanged;
        public event Action<int> DamageApplied;
        private GameSession session;
        private float invulnerableUntil;
        private int lastNotifiedMaxHealth;

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

        private void OnEnable()
        {
            session = GameSession.Instance;
            if (session != null)
            {
                session.Changed += SyncSessionHealth;
                SyncSessionHealth();
            }
        }

        private void OnDisable()
        {
            if (session != null) session.Changed -= SyncSessionHealth;
            session = null;
        }

        private void LateUpdate()
        {
            if (shieldSource == null) return;
            bool visible = session != null && session.ShieldCharges > 0 && shieldSource.enabled;
            foreach (SpriteRenderer outline in shieldOutline)
            {
                if (outline == null) continue;
                outline.enabled = visible;
                if (!visible) continue;
                outline.sprite = shieldSource.sprite;
                outline.flipX = shieldSource.flipX;
                outline.flipY = shieldSource.flipY;
                outline.sortingLayerID = shieldSource.sortingLayerID;
                outline.sortingOrder = shieldSource.sortingOrder - 1;
            }
        }

        private void SyncSessionHealth()
        {
            int maximum = MaxHealth;
            if (CurrentHealth == session.PlayerHealth && lastNotifiedMaxHealth == maximum) return;
            CurrentHealth = session.PlayerHealth;
            lastNotifiedMaxHealth = maximum;
            HealthChanged?.Invoke(CurrentHealth, maximum);
        }

        public bool TakeDamage(int amount) => TakeDamage(amount, 0f);

        public bool TakeDamage(int amount, float invulnerabilitySeconds)
        {
            if (!GameFlow.IsGameplayActive || amount <= 0 || CurrentHealth == 0 || Time.time < invulnerableUntil)
                return false;
            if (session != null && session.ShieldCharges > 0)
            {
                invulnerableUntil = Time.time + Mathf.Max(shieldHitProtection, invulnerabilitySeconds);
                session.ConsumeShieldCharge();
                GameAudio.Play(session.ShieldCharges == 0 ? Cue.ShieldBreak : Cue.Shield);
                return true;
            }
            invulnerableUntil = Time.time + Mathf.Max(0f, invulnerabilitySeconds);
            int appliedDamage = Mathf.Min(amount, CurrentHealth);
            CurrentHealth -= appliedDamage;
            if (CurrentHealth == 0) GameFlow.Instance?.PlayerDied();
            else GameAudio.Play(Cue.PlayerPain);
            GameSession.Instance?.UpdatePlayerHealth(CurrentHealth, MaxHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            DamageApplied?.Invoke(appliedDamage);
            if (CurrentHealth == 0)
                Destroy(gameObject);
            return true;
        }

        public void SetMaxHealth(int maximum)
        {
            maximum = session != null && session.HasPowerRing ? 1 : Mathf.Max(1, maximum);
            if (MaxHealth == maximum) return;
            // Keep absolute current HP: buying maximum HP does not heal.
            CurrentHealth = Mathf.Min(CurrentHealth, maximum);
            GameSession.Instance.UpdatePlayerHealth(CurrentHealth, maximum);
        }
    }
}
