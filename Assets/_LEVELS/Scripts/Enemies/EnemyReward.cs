using Levels.Core;
using UnityEngine;

namespace Levels.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class EnemyReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int experience = 10;
        [SerializeField, Min(0)] private int gold = 5;

        private bool granted;

        private int? progressionExperience;
        private int? progressionGold;
        public int BaseExperience => experience;
        public int BaseGold => gold;
        public int Experience => progressionExperience ?? experience;
        public int Gold => progressionGold ?? gold;

        public void SetProgressionReward(int exp, int currency)
        {
            progressionExperience = Mathf.Max(0, exp);
            progressionGold = Mathf.Max(0, currency);
        }

        public void GrantOnce()
        {
            if (granted) return;
            GameSession session = GameSession.Instance;
            if (session == null)
            {
                Debug.LogError("Enemy reward requires an active GameSession.", this);
                return;
            }

            granted = true;
            session.AddExperience(Experience);
            session.AddGold(Gold);
        }
    }
}
