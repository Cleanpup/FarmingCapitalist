using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Tracks cumulative purchased quantities for the current in-game day.
    /// Data is keyed by shop ID and item ID.
    /// </summary>
    internal static class DailyPurchaseTracker
    {
        internal static IMonitor? Monitor;

        private static readonly Dictionary<string, Dictionary<string, int>> PurchasedByShop = new(StringComparer.OrdinalIgnoreCase);
        private const string UnknownShopId = "<unknown-shop>";

        public static int GetPurchasedToday(string? shopId, ISalable item)
        {
            if (!BulkBuyRampRules.TryGetItemKey(item, out string itemKey))
                return 0;

            string resolvedShopId = NormalizeShopId(shopId);
            if (!PurchasedByShop.TryGetValue(resolvedShopId, out Dictionary<string, int>? shopMap))
                return 0;

            return shopMap.TryGetValue(itemKey, out int existingCount) ? existingCount : 0;
        }

        public static void RecordPurchase(string? shopId, ISalable item, int quantity)
        {
            if (quantity <= 0)
                return;

            if (!BulkBuyRampRules.TryGetItemKey(item, out string itemKey))
                return;

            string resolvedShopId = NormalizeShopId(shopId);
            if (!PurchasedByShop.TryGetValue(resolvedShopId, out Dictionary<string, int>? shopMap))
            {
                shopMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                PurchasedByShop[resolvedShopId] = shopMap;
            }

            shopMap.TryGetValue(itemKey, out int existingCount);
            int updatedCount = existingCount + quantity;
            shopMap[itemKey] = updatedCount;

            Monitor?.Log(
                $"DailyPurchaseTracker recorded {quantity} of {itemKey} in shop {resolvedShopId}. New total: {updatedCount}.",
                LogLevel.Trace
            );
        }

        public static void ResetForNewDay()
        {
            if (PurchasedByShop.Count > 0)
                Monitor?.Log("DailyPurchaseTracker reset for new day.", LogLevel.Trace);

            PurchasedByShop.Clear();
        }

        public static IReadOnlyDictionary<string, int> GetSnapshotForShop(string? shopId)
        {
            string resolvedShopId = NormalizeShopId(shopId);
            if (!PurchasedByShop.TryGetValue(resolvedShopId, out Dictionary<string, int>? shopMap))
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, int>(shopMap, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeShopId(string? shopId)
        {
            return string.IsNullOrWhiteSpace(shopId) ? UnknownShopId : shopId;
        }
    }
}
