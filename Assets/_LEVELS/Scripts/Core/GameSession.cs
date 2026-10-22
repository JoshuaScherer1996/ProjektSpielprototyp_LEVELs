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
        public int PlayerLevel { get; private set; } = 1;
        public int EnemyLevel { get; private set; } = 1;
        public int ShopMaxHealthBonus { get; private set; }
        public ProgressionSettings Progression { get; private set; }
        public int NextPlayerLevelExperience => Progression.PlayerCost(PlayerLevel);
        public int NextEnemyLevelExperience => Progression.EnemyCost(EnemyLevel);

        public event Action Changed;

        private bool playerHealthInitialized;
        private bool purchasingLevel;
        private bool purchasingShop;

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
            Progression = Resources.Load<ProgressionSettings>("LevelsProgression");
            if (Progression == null)
                Debug.LogError("Missing Resources/LevelsProgression progression settings.", this);
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
            PlayerLevel = 1;
            EnemyLevel = 1;
            ShopMaxHealthBonus = 0;
            purchasingLevel = false;
            purchasingShop = false;
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

        public bool TryLevelUpPlayer() => TryBuyLevel(true);
        public bool TryLevelUpEnemies() => TryBuyLevel(false);

        public int CalculatePlayerMaxHealth(int baseHealth) => (int)Math.Min(int.MaxValue,
            (long)Progression.PlayerHealth(baseHealth, PlayerLevel) + ShopMaxHealthBonus);

        internal bool TryBuyHealing(int price)
        {
            if (purchasingShop || price < 0 || CurrentGold < price || !playerHealthInitialized ||
                PlayerHealth <= 0 || PlayerHealth >= PlayerMaxHealth) return false;
            return CompleteShopPurchase(price, false);
        }

        internal bool TryBuyMaxHealth(int price)
        {
            if (purchasingShop || price < 0 || CurrentGold < price || !playerHealthInitialized ||
                PlayerHealth <= 0 || PlayerMaxHealth == int.MaxValue || ShopMaxHealthBonus == int.MaxValue)
                return false;
            return CompleteShopPurchase(price, true);
        }

        private bool CompleteShopPurchase(int price, bool increaseMaximum)
        {
            // Debit and effect form one transaction before any observer can react.
            purchasingShop = true;
            CurrentGold -= price;
            if (increaseMaximum)
            {
                ShopMaxHealthBonus++;
                PlayerMaxHealth++;
                // Match M5: increasing maximum HP keeps absolute current HP unchanged.
            }
            else PlayerHealth = PlayerMaxHealth;
            try { Changed?.Invoke(); }
            finally { purchasingShop = false; }
            return true;
        }

        private bool TryBuyLevel(bool player)
        {
            if (purchasingLevel || Progression == null) return false;
            int level = player ? PlayerLevel : EnemyLevel;
            int cost = player ? NextPlayerLevelExperience : NextEnemyLevelExperience;
            if (level == int.MaxValue || cost <= 0 || CurrentExperience < cost) return false;

            // Debit and increment atomically before notifying listeners; reentrant purchases are rejected.
            purchasingLevel = true;
            CurrentExperience -= cost;
            if (player) PlayerLevel++;
            else EnemyLevel++;
            try { Changed?.Invoke(); }
            finally { purchasingLevel = false; }
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
