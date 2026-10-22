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

        public int Experience => experience;
        public int Gold => gold;

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
            session.AddExperience(experience);
            session.AddGold(gold);
        }
    }
}
