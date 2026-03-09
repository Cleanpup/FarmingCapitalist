using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Exact per-crop tuning layer.
    /// Values are intentionally neutral (1f) by default so behavior is unchanged until edited.
    /// </summary>
    internal static class CropItemEconomyRules
    {
        private readonly record struct CropItemMultiplier(float BuyMultiplier, float SellMultiplier);

        // Editable crop-by-crop overrides keyed by crop seed item ID.
        // Add/edit entries here for targeted exceptions without touching trait logic.
        private static readonly Dictionary<string, CropItemMultiplier> SeedOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            // Spring
            ["472"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Parsnip Seeds
            ["473"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Bean Starter
            ["474"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Cauliflower Seeds
            ["475"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Potato Seeds
            ["745"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Strawberry Seeds

            // Summer
            ["478"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Blueberry Seeds
            ["480"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Tomato Seeds
            ["479"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Melon Seeds
            ["487"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Corn Seeds
            ["486"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Starfruit Seeds

            // Fall
            ["490"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Pumpkin Seeds
            ["493"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f), // Cranberry Seeds
            ["301"] = new CropItemMultiplier(BuyMultiplier: 1f, SellMultiplier: 1f)  // Grape Starter
        };

        public static float GetBuyItemModifier(Item item, EconomyContext context)
        {
            _ = context;

            if (!TryResolveSeedItemId(item, out string seedItemId))
                return 1f;

            if (!SeedOverrides.TryGetValue(seedItemId, out CropItemMultiplier overrideValue))
                return 1f;

            return Math.Max(0f, overrideValue.BuyMultiplier);
        }

        public static float GetSellItemModifier(Item item, EconomyContext context)
        {
            _ = context;

            if (!TryResolveSeedItemId(item, out string seedItemId))
                return 1f;

            if (!SeedOverrides.TryGetValue(seedItemId, out CropItemMultiplier overrideValue))
                return 1f;

            return Math.Max(0f, overrideValue.SellMultiplier);
        }

        private static bool TryResolveSeedItemId(Item? item, out string seedItemId)
        {
            seedItemId = string.Empty;

            if (item is null)
                return false;

            return CropTraitService.TryGetCropData(item, out seedItemId, out _);
        }
    }
}
