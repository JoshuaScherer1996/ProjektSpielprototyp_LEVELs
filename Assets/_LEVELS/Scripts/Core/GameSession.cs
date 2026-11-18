using System;
using UnityEngine;

namespace Levels.Core
{
    public readonly struct VictoryLevels
    {
        public readonly int Player;
        public readonly int Goblin;
        public readonly int Cyclops;

        public VictoryLevels(int player, int goblin, int cyclops)
        {
            Player = player;
            Goblin = goblin;
            Cyclops = cyclops;
        }
    }

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
        // Retain the original storage and API: existing session state and callers become Goblin progression.
        public int GoblinLevel => EnemyLevel;
        public int CyclopsLevel { get; private set; } = 1;
        public VictoryLevels? Victory { get; private set; }
        public int ShopMaxHealthBonus { get; private set; }
        public const int MaxShieldCharges = 3;
        public int ShieldCharges { get; private set; }
        public bool HasPowerRing { get; private set; }
        public int PotionCount { get; private set; }
        public ProgressionSettings Progression { get; private set; }
        public int NextPlayerLevelExperience => Progression.PlayerCost(PlayerLevel);
        public int NextEnemyLevelExperience => Progression.EnemyCost(EnemyLevel);
        public int NextGoblinLevelExperience => NextEnemyLevelExperience;
        public int NextCyclopsLevelExperience => Progression.EnemyCost(CyclopsLevel, EnemyType.Cyclops);

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
            CyclopsLevel = 1;
            Victory = null;
            ShopMaxHealthBonus = 0;
            ShieldCharges = 0;
            HasPowerRing = false;
            PotionCount = 0;
            purchasingLevel = false;
            purchasingShop = false;
            playerHealthInitialized = false;
            // Existing scene observers unsubscribe on unload. Never erase their subscriptions
            // during a run reset, or the destination could silently lose UI/stat notifications.
        }

        public void ResetForNewRun() => ResetData();
        public void EndRun() => ResetData();
        public void RecordVictory() => Victory = new VictoryLevels(PlayerLevel, GoblinLevel, CyclopsLevel);

        public void InitializePlayerHealth(int defaultMaxHealth)
        {
            if (playerHealthInitialized) return;
            UpdatePlayerHealth(defaultMaxHealth, defaultMaxHealth);
        }

        public void AddExperience(int amount)
        {
            if (!GameFlow.IsGameplayActive || amount <= 0) return;
            int experience = (int)Math.Min(int.MaxValue, (long)CurrentExperience + amount);
            int total = (int)Math.Min(int.MaxValue, (long)TotalExperience + amount);
            if (experience == CurrentExperience && total == TotalExperience) return;
            CurrentExperience = experience;
            TotalExperience = total;
            Changed?.Invoke();
        }

        public void AddGold(int amount)
        {
            if (!GameFlow.IsGameplayActive || amount <= 0) return;
            int gold = (int)Math.Min(int.MaxValue, (long)CurrentGold + amount);
            if (gold == CurrentGold) return;
            CurrentGold = gold;
            Changed?.Invoke();
        }

        public bool TrySpendExperience(int amount)
        {
            if (!GameFlow.IsGameplayActive || amount < 0 || amount > CurrentExperience) return false;
            if (amount == 0) return true;
            CurrentExperience -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendGold(int amount)
        {
            if (!GameFlow.IsGameplayActive || amount < 0 || amount > CurrentGold) return false;
            if (amount == 0) return true;
            CurrentGold -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TryLevelUpPlayer() => TryBuyLevel(null);
        public bool TryLevelUpEnemies() => TryLevelUpGoblin();
        public bool TryLevelUpGoblin() => TryBuyLevel(EnemyType.Goblin);
        public bool TryLevelUpCyclops() => TryBuyLevel(EnemyType.Cyclops);
        public int GetEnemyLevel(EnemyType type) => type == EnemyType.Cyclops ? CyclopsLevel : GoblinLevel;

        public int CalculatePlayerMaxHealth(int baseHealth) => HasPowerRing ? 1 : (int)Math.Min(int.MaxValue,
            (long)Progression.PlayerHealth(baseHealth, PlayerLevel) + ShopMaxHealthBonus);

        internal ShopPurchaseResult GetOfferStatus(ShopOffer offer, long price)
        {
            if (!GameFlow.IsGameplayActive || price < 0 || !playerHealthInitialized || PlayerHealth <= 0)
                return ShopPurchaseResult.Unavailable;
            switch (offer)
            {
                case ShopOffer.Healing when PlayerHealth >= PlayerMaxHealth:
                    return ShopPurchaseResult.FullHealth;
                case ShopOffer.MaxHealth when HasPowerRing:
                case ShopOffer.Potion when HasPowerRing:
                    return ShopPurchaseResult.BlockedByRing;
                case ShopOffer.MaxHealth when PlayerMaxHealth == int.MaxValue || ShopMaxHealthBonus == int.MaxValue:
                case ShopOffer.Potion when PotionCount == int.MaxValue:
                    return ShopPurchaseResult.LimitReached;
                case ShopOffer.Shield when ShieldCharges > 0:
                case ShopOffer.PowerRing when HasPowerRing:
                    return ShopPurchaseResult.AlreadyActive;
            }
            return CurrentGold < price ? ShopPurchaseResult.NotEnoughGold : ShopPurchaseResult.Success;
        }

        internal ShopPurchaseResult TryBuyOffer(ShopOffer offer, long price)
        {
            if (purchasingShop) return ShopPurchaseResult.Busy;
            ShopPurchaseResult status = GetOfferStatus(offer, price);
            if (status != ShopPurchaseResult.Success) return status;

            purchasingShop = true;
            CurrentGold -= (int)price;
            switch (offer)
            {
                case ShopOffer.Healing:
                    PlayerHealth = PlayerMaxHealth;
                    break;
                case ShopOffer.MaxHealth:
                    ShopMaxHealthBonus++;
                    PlayerMaxHealth++;
                    break;
                case ShopOffer.Shield:
                    ShieldCharges = MaxShieldCharges;
                    break;
                case ShopOffer.PowerRing:
                    HasPowerRing = true;
                    PlayerMaxHealth = PlayerHealth = 1;
                    break;
                case ShopOffer.Potion:
                    PotionCount++;
                    break;
            }
            try { Changed?.Invoke(); }
            finally { purchasingShop = false; }
            return ShopPurchaseResult.Success;
        }

        internal void ConsumeShieldCharge()
        {
            if (ShieldCharges == 0) return;
            ShieldCharges--;
            Changed?.Invoke();
        }

        internal bool TryUsePotion()
        {
            if (!GameFlow.IsGameplayActive || HasPowerRing || PotionCount <= 0 ||
                PlayerHealth <= 0 || PlayerHealth >= PlayerMaxHealth) return false;

            int healing = (int)((PlayerMaxHealth + 4L) / 5);
            PotionCount--;
            PlayerHealth += Mathf.Min(healing, PlayerMaxHealth - PlayerHealth);
            Changed?.Invoke();
            return true;
        }

        private bool TryBuyLevel(EnemyType? type)
        {
            if (!GameFlow.IsGameplayActive || purchasingLevel || Progression == null) return false;
            int level = type.HasValue ? GetEnemyLevel(type.Value) : PlayerLevel;
            int cost = type.HasValue ? Progression.EnemyCost(level, type.Value) : NextPlayerLevelExperience;
            if (level == int.MaxValue || cost <= 0 || CurrentExperience < cost) return false;

            // Debit and increment atomically before notifying listeners; reentrant purchases are rejected.
            purchasingLevel = true;
            CurrentExperience -= cost;
            if (!type.HasValue) PlayerLevel++;
            else if (type.Value == EnemyType.Cyclops) CyclopsLevel++;
            else EnemyLevel++;
            try { Changed?.Invoke(); }
            finally { purchasingLevel = false; }
            return true;
        }

        public void UpdatePlayerHealth(int currentHealth, int maxHealth)
        {
            int maximum = HasPowerRing ? 1 : Mathf.Max(1, maxHealth);
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
