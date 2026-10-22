using Levels.Core;
using UnityEngine;

namespace Levels.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerAttack))]
    public sealed class PlayerProgression : MonoBehaviour
    {
        private GameSession session;
        private PlayerHealth health;
        private PlayerAttack attack;

        private void Start()
        {
            session = GameSession.Instance;
            health = GetComponent<PlayerHealth>();
            attack = GetComponent<PlayerAttack>();
            session.Changed += ApplyStats;
            ApplyStats();
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= ApplyStats;
        }

        private void ApplyStats()
        {
            attack.SetProgressionDamage(session.Progression.PlayerDamage(attack.BaseDamage, session.PlayerLevel));
            health.SetMaxHealth(session.CalculatePlayerMaxHealth(health.BaseMaxHealth));
        }

        public bool TryLevelUp() => GameSession.Instance.TryLevelUpPlayer();
    }
}
