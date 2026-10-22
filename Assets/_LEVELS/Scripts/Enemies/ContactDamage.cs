using Levels.Player;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(Collider2D))]
    public sealed class ContactDamage : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.75f;

        private float nextDamageTime;
        private int progressionDamage;
        public int BaseDamage => damage;
        public int Damage => progressionDamage > 0 ? progressionDamage : damage;
        public void SetProgressionDamage(int value) => progressionDamage = Mathf.Max(1, value);

        private void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryDamage(collision);

        private void TryDamage(Collision2D collision)
        {
            if (Time.time < nextDamageTime) return;
            PlayerHealth player = collision.collider.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.TakeDamage(Damage)) return;
            nextDamageTime = Time.time + cooldownSeconds;
        }
    }
}
