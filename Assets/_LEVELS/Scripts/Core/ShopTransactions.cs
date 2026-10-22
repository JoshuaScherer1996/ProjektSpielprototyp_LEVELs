using Levels.Player;
using UnityEngine;

namespace Levels.Core
{
    public enum ShopOffer { Healing, MaxHealth }
    public enum ShopPurchaseResult { Success, Unavailable, NotEnoughGold, FullHealth, LimitReached, Busy }

    [DisallowMultipleComponent]
    public sealed class ShopTransactions : MonoBehaviour
    {
        [SerializeField, Min(0)] private int healingPrice = 5;
        [SerializeField, Min(0)] private int maxHealthPrice = 25;

        public int HealingPrice => healingPrice;
        public int MaxHealthPrice => maxHealthPrice;

        public ShopPurchaseResult TryPurchase(ShopOffer offer, PlayerHealth player)
        {
            GameSession session = GameSession.Instance;
            if (!isActiveAndEnabled || player == null || player.CurrentHealth <= 0 || session == null)
                return ShopPurchaseResult.Unavailable;
            if (offer != ShopOffer.Healing && offer != ShopOffer.MaxHealth)
                return ShopPurchaseResult.Unavailable;

            bool healing = offer == ShopOffer.Healing;
            int price = healing ? healingPrice : maxHealthPrice;
            if (price < 0) return ShopPurchaseResult.Unavailable;
            if (healing && session.PlayerHealth >= session.PlayerMaxHealth)
                return ShopPurchaseResult.FullHealth;
            if (!healing && session.PlayerMaxHealth == int.MaxValue)
                return ShopPurchaseResult.LimitReached;
            if (session.CurrentGold < price) return ShopPurchaseResult.NotEnoughGold;

            bool bought = healing ? session.TryBuyHealing(price) : session.TryBuyMaxHealth(price);
            return bought ? ShopPurchaseResult.Success : ShopPurchaseResult.Busy;
        }
    }
}
