using Levels.Core;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(ContactDamage), typeof(EnemyReward))]
    public sealed class EnemyProgression : MonoBehaviour
    {
        [SerializeField] private EnemyType enemyType;
        public EnemyType Type => enemyType;
        public int Level { get; private set; } = 1;

        private void Start()
        {
            GameSession session = GameSession.Instance;
            Level = session.GetEnemyLevel(enemyType);
            ProgressionSettings settings = session.Progression;
            EnemyHealth health = GetComponent<EnemyHealth>();
            ContactDamage contact = GetComponent<ContactDamage>();
            EnemyReward reward = GetComponent<EnemyReward>();
            health.SetInitialMaxHealth(settings.EnemyHealth(health.BaseMaxHealth, Level, enemyType));
            contact.SetProgressionDamage(settings.EnemyDamage(contact.BaseDamage, Level, enemyType));
            reward.SetProgressionReward(settings.EnemyExperience(reward.BaseExperience, Level, enemyType),
                settings.EnemyGold(reward.BaseGold, Level, enemyType));
        }
    }
}
