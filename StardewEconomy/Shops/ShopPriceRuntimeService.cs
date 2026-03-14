using System.Runtime.CompilerServices;
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
        private sealed class CanonicalStockEntry
        {
            public int VanillaPrice { get; init; }
        }

        private sealed class ShopSessionState
        {
            public string ShopId { get; init; } = string.Empty;
            public float ShopPriceMultiplier { get; init; }
            public EconomyContext Context { get; init; } = new();
            public Dictionary<ISalable, int> VanillaPrices { get; init; } = new();
            public Dictionary<string, CanonicalStockEntry> CanonicalVanillaBuyPrices { get; init; } = new(StringComparer.OrdinalIgnoreCase);
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
            Dictionary<string, CanonicalStockEntry> canonicalVanillaBuyPrices = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<ISalable, ItemStockInformation> pair in shop.itemPriceAndStock)
            {
                vanillaPrices[pair.Key] = pair.Value.Price;
                if (TryGetComparableItemKey(pair.Key, out string itemKey)
                    && !canonicalVanillaBuyPrices.ContainsKey(itemKey)
                    && !IsPlayerSoldStock(pair.Value))
                {
                    canonicalVanillaBuyPrices[itemKey] = new CanonicalStockEntry
                    {
                        VanillaPrice = pair.Value.Price
                    };
                }
            }

            ShopSessionState state = new ShopSessionState
            {
                ShopId = shop.ShopId ?? string.Empty,
                ShopPriceMultiplier = shopPriceMultiplier,
                Context = context,
                VanillaPrices = vanillaPrices,
                CanonicalVanillaBuyPrices = canonicalVanillaBuyPrices
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

            int quantity = Math.Max(1, pendingQuantity);
            CurrentPendingPurchase = new PendingPurchaseState
            {
                Shop = shop,
                Item = item,
                Quantity = quantity
            };

            ApplyPriceToItem(shop, state, item, quantity);
        }

        public static void MarkPurchaseCharged(int amount)
        {
            if (CurrentPendingPurchase is null || amount < 0)
                return;

            CurrentPendingPurchase.WasCharged = true;
        }

        public static void CompletePurchaseAttempt(ShopMenu shop)
        {
            if (CurrentPendingPurchase is not null && ReferenceEquals(CurrentPendingPurchase.Shop, shop))
            {
                if (CurrentPendingPurchase.WasCharged)
                    RecordConfirmedPurchase(shop, CurrentPendingPurchase.Item, CurrentPendingPurchase.Quantity);

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

        public static bool TryGetCurrentAdjustedBuyPrice(ShopMenu shop, Item item, out int adjustedPrice)
        {
            adjustedPrice = 0;

            if (shop.currency != 0)
                return false;

            if (!TryGetComparableItemKey(item, out string itemKey))
                return false;

            if (!TryGetState(shop, out ShopSessionState state))
                return false;

            if (!state.CanonicalVanillaBuyPrices.TryGetValue(itemKey, out CanonicalStockEntry? canonicalEntry) || canonicalEntry is null)
                return false;

            adjustedPrice = Math.Max(1, GetAdjustedPriceForVanillaBaseline(state, item, canonicalEntry.VanillaPrice, pendingQuantity: 1));
            return true;
        }

        public static void Clear()
        {
            Sessions = new ConditionalWeakTable<ShopMenu, ShopSessionState>();
            CurrentPendingPurchase = null;
        }

        private static void RecordConfirmedPurchase(ShopMenu shop, ISalable item, int purchasedQuantity)
        {
            if (!TryGetState(shop, out ShopSessionState state))
                return;

            DailyPurchaseTracker.RecordPurchase(state.ShopId, item, purchasedQuantity);
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

            int adjusted = GetAdjustedPriceForItem(state, item, stock, pendingQuantity);
            stock.Price = Math.Max(1, adjusted);
            shop.itemPriceAndStock[item] = stock;
        }

        private static int GetAdjustedPriceForItem(ShopSessionState state, ISalable item, ItemStockInformation stock, int pendingQuantity)
        {
            int vanillaPrice = GetVanillaPrice(state, item, stock.Price);
            return GetAdjustedPriceForVanillaBaseline(state, item, vanillaPrice, pendingQuantity);
        }

        private static int GetAdjustedPriceForVanillaBaseline(ShopSessionState state, ISalable item, int vanillaPrice, int pendingQuantity)
        {
            int basePrice = (int)Math.Round(vanillaPrice * state.ShopPriceMultiplier, MidpointRounding.AwayFromZero);
            int purchasedToday = DailyPurchaseTracker.GetPurchasedToday(state.ShopId, item);

            return EconomyService.AdjustBuyPrice(
                vanillaPrice: basePrice,
                item: item,
                shopId: state.ShopId,
                context: state.Context,
                cumulativePurchasedToday: purchasedToday,
                purchaseQuantity: pendingQuantity
            );
        }

        private static int GetVanillaPrice(ShopSessionState state, ISalable item, int fallbackPrice)
        {
            if (!state.VanillaPrices.TryGetValue(item, out int vanillaPrice))
            {
                vanillaPrice = fallbackPrice;
                state.VanillaPrices[item] = vanillaPrice;
            }

            return vanillaPrice;
        }

        private static bool IsPlayerSoldStock(ItemStockInformation stock)
        {
            return !string.IsNullOrWhiteSpace(stock.SyncedKey)
                && stock.SyncedKey.StartsWith("ITEMS_SOLD_BY_PLAYER", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetComparableItemKey(ISalable item, out string itemKey)
        {
            itemKey = string.Empty;

            if (item is not Item asItem)
                return false;

            if (!string.IsNullOrWhiteSpace(asItem.QualifiedItemId))
            {
                itemKey = asItem.QualifiedItemId;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(asItem.ItemId))
            {
                itemKey = asItem.ItemId;
                return true;
            }

            return false;
        }
    }
}
