using Levels.Core;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(ContactDamage), typeof(EnemyReward))]
    public sealed class EnemyProgression : MonoBehaviour
    {
        public int Level { get; private set; } = 1;

        private void Start()
        {
            GameSession session = GameSession.Instance;
            Level = session.EnemyLevel;
            ProgressionSettings settings = session.Progression;
            EnemyHealth health = GetComponent<EnemyHealth>();
            ContactDamage contact = GetComponent<ContactDamage>();
            EnemyReward reward = GetComponent<EnemyReward>();
            health.SetInitialMaxHealth(settings.EnemyHealth(health.BaseMaxHealth, Level));
            contact.SetProgressionDamage(settings.EnemyDamage(contact.BaseDamage, Level));
            reward.SetProgressionReward(settings.EnemyExperience(reward.BaseExperience, Level),
                settings.EnemyGold(reward.BaseGold, Level));
        }
    }
}
