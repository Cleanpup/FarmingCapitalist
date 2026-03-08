using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace FarmingCapitalist
{
    /// <summary>
    /// Runtime state for currently opened shops. Keeps baseline prices stable and
    /// reapplies dynamic pricing as daily purchase totals change.
    /// </summary>
    internal static class ShopPriceRuntimeService
    {
        internal static IMonitor? Monitor;

        private sealed class ShopSessionState
        {
            public string ShopId { get; init; } = string.Empty;
            public float ShopPriceMultiplier { get; init; }
            public EconomyContext Context { get; init; } = new();
            public Dictionary<ISalable, int> VanillaPrices { get; init; } = new();
        }

        private sealed class PendingPurchaseState
        {
            public ShopMenu Shop { get; init; } = null!;
            public ISalable Item { get; init; } = null!;
            public int Quantity { get; init; }
            public bool WasCharged { get; set; }
        }

        private static ConditionalWeakTable<ShopMenu, ShopSessionState> Sessions = new();
        [ThreadStatic]
        private static PendingPurchaseState? CurrentPendingPurchase;

        public static void AttachShop(ShopMenu shop, float shopPriceMultiplier, EconomyContext context)
        {
            if (Sessions.TryGetValue(shop, out _))
                Sessions.Remove(shop);

            Dictionary<ISalable, int> vanillaPrices = new();
            foreach (KeyValuePair<ISalable, ItemStockInformation> pair in shop.itemPriceAndStock)
            {
                vanillaPrices[pair.Key] = pair.Value.Price;
            }

            ShopSessionState state = new ShopSessionState
            {
                ShopId = shop.ShopId ?? string.Empty,
                ShopPriceMultiplier = shopPriceMultiplier,
                Context = context,
                VanillaPrices = vanillaPrices
            };

            Sessions.Add(shop, state);
            RefreshDisplayPrices(shop);
        }

        public static void BeginPurchaseAttempt(ShopMenu shop, ISalable item, int pendingQuantity)
        {
            if (!TryGetState(shop, out ShopSessionState state))
                return;

            if (shop.currency != 0)
                return;

            CurrentPendingPurchase = new PendingPurchaseState
            {
                Shop = shop,
                Item = item,
                Quantity = Math.Max(1, pendingQuantity)
            };

            ApplyPriceToItem(shop, state, item, Math.Max(1, pendingQuantity));
        }

        public static void MarkPurchaseCharged(int amount)
        {
            if (amount < 0 || CurrentPendingPurchase is null)
                return;

            CurrentPendingPurchase.WasCharged = true;
        }

        public static void CompletePurchaseAttempt(ShopMenu shop)
        {
            if (CurrentPendingPurchase is not null && ReferenceEquals(CurrentPendingPurchase.Shop, shop))
            {
                if (CurrentPendingPurchase.WasCharged)
                {
                    RecordConfirmedPurchase(shop, CurrentPendingPurchase.Item, CurrentPendingPurchase.Quantity);
                }

                CurrentPendingPurchase = null;
            }

            RefreshDisplayPrices(shop);
        }

        public static void RefreshDisplayPrices(ShopMenu shop)
        {
            if (!TryGetState(shop, out ShopSessionState state))
                return;

            if (shop.currency != 0)
                return;

            foreach (ISalable item in shop.itemPriceAndStock.Keys.ToList())
            {
                ApplyPriceToItem(shop, state, item, pendingQuantity: 1);
            }
        }

        private static void RecordConfirmedPurchase(ShopMenu shop, ISalable item, int purchasedQuantity)
        {
            if (!TryGetState(shop, out ShopSessionState state))
                return;

            DailyPurchaseTracker.RecordPurchase(state.ShopId, item, purchasedQuantity);
        }

        public static void Clear()
        {
            Sessions = new ConditionalWeakTable<ShopMenu, ShopSessionState>();
        }

        private static bool TryGetState(ShopMenu shop, out ShopSessionState state)
        {
            if (!Sessions.TryGetValue(shop, out ShopSessionState? maybeState) || maybeState is null)
            {
                state = null!;
                return false;
            }

            state = maybeState;
            return true;
        }

        private static void ApplyPriceToItem(ShopMenu shop, ShopSessionState state, ISalable item, int pendingQuantity)
        {
            if (!shop.itemPriceAndStock.TryGetValue(item, out ItemStockInformation? stock) || stock is null)
                return;

            if (!state.VanillaPrices.TryGetValue(item, out int vanillaPrice))
            {
                vanillaPrice = stock.Price;
                state.VanillaPrices[item] = vanillaPrice;
            }

            int basePrice = (int)Math.Round(vanillaPrice * state.ShopPriceMultiplier, MidpointRounding.AwayFromZero);
            int purchasedToday = DailyPurchaseTracker.GetPurchasedToday(state.ShopId, item);
            int adjusted = EconomyService.AdjustBuyPrice(
                vanillaPrice: basePrice,
                item: item,
                shopId: state.ShopId,
                context: state.Context,
                cumulativePurchasedToday: purchasedToday,
                purchaseQuantity: pendingQuantity
            );

            stock.Price = Math.Max(1, adjusted);
            shop.itemPriceAndStock[item] = stock;
        }
    }
}
