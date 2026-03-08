using StardewValley;

namespace FarmingCapitalist
{
    internal readonly record struct BulkBuyRampSettings(int Threshold, float Slope, float MaxMultiplier);

    /// <summary>
    /// Rules for bulk-buy ramp behavior, including shop-specific overrides.
    /// </summary>
    internal static class BulkBuyRampRules
    {
        private static readonly BulkBuyRampSettings DefaultSettings = new(Threshold: 50, Slope: 0.002f, MaxMultiplier: 1.90f);

        private static readonly Dictionary<string, BulkBuyRampSettings> ShopOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SeedShop"] = new BulkBuyRampSettings(Threshold: 50, Slope: 0.002f, MaxMultiplier: 1.90f),
            ["Joja"] = new BulkBuyRampSettings(Threshold: 50, Slope: 0.002f, MaxMultiplier: 1.90f),
            ["JojaMart"] = new BulkBuyRampSettings(Threshold: 50, Slope: 0.002f, MaxMultiplier: 1.90f)
        };

        public static BulkBuyRampSettings ResolveSettings(string? shopId)
        {
            if (!string.IsNullOrWhiteSpace(shopId) && ShopOverrides.TryGetValue(shopId, out BulkBuyRampSettings settings))
                return settings;

            return DefaultSettings;
        }

        public static bool TryGetItemKey(ISalable item, out string itemKey)
        {
            itemKey = string.Empty;

            if (item is not Item asItem)
                return false;

            if (!string.IsNullOrWhiteSpace(asItem.ItemId))
            {
                itemKey = asItem.ItemId;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(asItem.QualifiedItemId))
            {
                itemKey = asItem.QualifiedItemId;
                return true;
            }

            return false;
        }

        public static float GetMultiplier(ISalable item, string? shopId, int cumulativePurchasedBefore, int purchaseQuantity)
        {
            if (!TryGetItemKey(item, out _))
                return 1f;

            BulkBuyRampSettings settings = ResolveSettings(shopId);
            int quantity = Math.Max(1, purchaseQuantity);
            int cumulativeAfterPurchase = Math.Max(0, cumulativePurchasedBefore) + quantity;
            int rampedQuantity = Math.Max(0, cumulativeAfterPurchase - settings.Threshold);

            float multiplier = 1f + (rampedQuantity * settings.Slope);
            return Math.Min(multiplier, settings.MaxMultiplier);
        }
    }
}
