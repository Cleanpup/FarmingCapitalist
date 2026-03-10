using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Exact per-crop tuning layer.
    /// This first-pass table applies targeted crop-level overrides on top of trait multipliers.
    /// </summary>
    internal static class CropItemEconomyRules
    {
        private readonly record struct CropItemMultiplier(float BuyMultiplier, float SellMultiplier);

        // Editable crop-by-crop overrides keyed by crop seed item ID.
        // Add/edit entries here for targeted exceptions without touching trait logic.
        // This is a first-pass balance table and will be tuned with playtest data.
        private static readonly Dictionary<string, CropItemMultiplier> SeedOverrides = new(StringComparer.OrdinalIgnoreCase)
        {
            // Spring
            ["472"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.20f), // Parsnip Seeds
            ["473"] = new CropItemMultiplier(BuyMultiplier: 0.33f, SellMultiplier: 1.50f), // Bean Starter (Green Bean)
            ["474"] = new CropItemMultiplier(BuyMultiplier: 0.75f, SellMultiplier: 1.60f), // Cauliflower Seeds
            ["475"] = new CropItemMultiplier(BuyMultiplier: 0.40f, SellMultiplier: 1.10f), // Potato Seeds
            ["477"] = new CropItemMultiplier(BuyMultiplier: 0.70f, SellMultiplier: 1.30f), // Kale Seeds
            ["476"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.50f), // Garlic Seeds
            ["478"] = new CropItemMultiplier(BuyMultiplier: 0.40f, SellMultiplier: 1.20f), // Rhubarb Seeds
            ["745"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.00f), // Strawberry Seeds
            ["273"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 3.00f), // Rice Shoot (Unmilled Rice)

            // Summer
            ["481"] = new CropItemMultiplier(BuyMultiplier: 1.25f, SellMultiplier: 1.00f), // Blueberry Seeds
            ["482"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.40f), // Pepper Seeds (Hot Pepper)
            ["479"] = new CropItemMultiplier(BuyMultiplier: 0.625f, SellMultiplier: 1.10f), // Melon Seeds
            ["480"] = new CropItemMultiplier(BuyMultiplier: 0.80f, SellMultiplier: 1.50f), // Tomato Seeds
            ["487"] = new CropItemMultiplier(BuyMultiplier: 0.13f, SellMultiplier: 1.80f), // Corn Seeds
            ["302"] = new CropItemMultiplier(BuyMultiplier: 1.33f, SellMultiplier: 1.00f), // Hops Starter
            ["483"] = new CropItemMultiplier(BuyMultiplier: 0.85f, SellMultiplier: 2.00f), // Wheat Seeds
            ["484"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.30f), // Radish Seeds
            ["485"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.95f), // Red Cabbage Seeds
            ["486"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.90f), // Starfruit Seeds

            // Fall
            ["493"] = new CropItemMultiplier(BuyMultiplier: 0.417f, SellMultiplier: 0.80f), // Cranberry Seeds
            ["488"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.50f), // Eggplant Seeds
            ["301"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.11f), // Grape Starter
            ["299"] = new CropItemMultiplier(BuyMultiplier: 0.786f, SellMultiplier: 1.00f), // Amaranth Seeds
            ["494"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.15f), // Beet Seeds
            ["492"] = new CropItemMultiplier(BuyMultiplier: 0.833f, SellMultiplier: 1.60f), // Yam Seeds
            ["490"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.95f), // Pumpkin Seeds
            ["489"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.10f), // Artichoke Seeds
            ["491"] = new CropItemMultiplier(BuyMultiplier: 0.50f, SellMultiplier: 1.00f), // Bok Choy Seeds
            ["347"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.00f)  // Rare Seed (Sweet Gem Berry)
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
