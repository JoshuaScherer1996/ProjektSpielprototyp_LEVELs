using System;
using UnityEngine;

namespace Levels.Core
{
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public int CurrentExperience { get; private set; }
        public int TotalExperience { get; private set; }
        public int CurrentGold { get; private set; }
        public int PlayerHealth { get; private set; }
        public int PlayerMaxHealth { get; private set; }

        public event Action Changed;

        private bool playerHealthInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticReference()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartNewRun()
        {
            GameSession[] sessions = FindObjectsByType<GameSession>(FindObjectsInactive.Include);
            if (sessions.Length == 0)
            {
                new GameObject("GameSession").AddComponent<GameSession>();
                return;
            }

            Instance = sessions[0];
            Instance.ResetData();
            DontDestroyOnLoad(Instance.gameObject);
            for (int i = 1; i < sessions.Length; i++)
                Destroy(sessions[i].gameObject);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ResetData()
        {
            CurrentExperience = 0;
            TotalExperience = 0;
            CurrentGold = 0;
            PlayerHealth = 0;
            PlayerMaxHealth = 0;
            playerHealthInitialized = false;
            Changed = null;
        }

        public void InitializePlayerHealth(int defaultMaxHealth)
        {
            if (playerHealthInitialized) return;
            UpdatePlayerHealth(defaultMaxHealth, defaultMaxHealth);
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0) return;
            int experience = (int)Math.Min(int.MaxValue, (long)CurrentExperience + amount);
            int total = (int)Math.Min(int.MaxValue, (long)TotalExperience + amount);
            if (experience == CurrentExperience && total == TotalExperience) return;
            CurrentExperience = experience;
            TotalExperience = total;
            Changed?.Invoke();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            int gold = (int)Math.Min(int.MaxValue, (long)CurrentGold + amount);
            if (gold == CurrentGold) return;
            CurrentGold = gold;
            Changed?.Invoke();
        }

        public bool TrySpendExperience(int amount)
        {
            if (amount < 0 || amount > CurrentExperience) return false;
            if (amount == 0) return true;
            CurrentExperience -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || amount > CurrentGold) return false;
            if (amount == 0) return true;
            CurrentGold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void UpdatePlayerHealth(int currentHealth, int maxHealth)
        {
            int maximum = Mathf.Max(1, maxHealth);
            int current = Mathf.Clamp(currentHealth, 0, maximum);
            if (playerHealthInitialized && PlayerHealth == current && PlayerMaxHealth == maximum)
                return;

            playerHealthInitialized = true;
            PlayerHealth = current;
            PlayerMaxHealth = maximum;
            Changed?.Invoke();
        }
    }
}
