using Levels.Player;
using UnityEngine;

namespace Levels.Core
{
    public enum ShopOffer { Healing, MaxHealth, Shield, PowerRing, Potion }
    public enum ShopPurchaseResult { Success, Unavailable, NotEnoughGold, FullHealth, LimitReached, Busy, AlreadyActive, BlockedByRing }

    [DisallowMultipleComponent]
    public sealed class ShopTransactions : MonoBehaviour
    {
        [SerializeField, Min(0)] private int healingPrice = 5;
        [SerializeField, Min(0)] private int maxHealthPrice = 25;
        [SerializeField, Min(0)] private int shieldPrice = 150;
        [SerializeField, Min(0)] private int powerRingPrice = 10000;

        public int HealingPrice => healingPrice;
        public int MaxHealthPrice => maxHealthPrice;

        public long GetPrice(ShopOffer offer, GameSession session) => offer switch
        {
            ShopOffer.Healing => HealingPrice,
            ShopOffer.MaxHealth => MaxHealthPrice,
            ShopOffer.Shield => shieldPrice,
            ShopOffer.PowerRing => powerRingPrice,
            ShopOffer.Potion => 50L + 25L * (session.PlayerLevel - 1),
            _ => -1
        };

        public ShopPurchaseResult TryPurchase(ShopOffer offer, PlayerHealth player)
        {
            GameSession session = GameSession.Instance;
            if (!isActiveAndEnabled || player == null || player.CurrentHealth <= 0 || session == null)
                return ShopPurchaseResult.Unavailable;
            return session.TryBuyOffer(offer, GetPrice(offer, session));
        }
    }
}
