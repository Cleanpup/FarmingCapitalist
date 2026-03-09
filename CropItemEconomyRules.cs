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
            ["472"] = new CropItemMultiplier(BuyMultiplier: 0.90f, SellMultiplier: 1.79f), // Parsnip Seeds
            ["473"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.86f), // Bean Starter (Green Bean)
            ["474"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.47f), // Cauliflower Seeds
            ["475"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.31f), // Potato Seeds
            ["477"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.18f), // Kale Seeds
            ["476"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.16f), // Garlic Seeds
            ["478"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.25f), // Rhubarb Seeds
            ["745"] = new CropItemMultiplier(BuyMultiplier: 1.08f, SellMultiplier: 0.97f), // Strawberry Seeds
            ["273"] = new CropItemMultiplier(BuyMultiplier: 0.90f, SellMultiplier: 3.20f), // Rice Shoot (Unmilled Rice)

            // Summer
            ["481"] = new CropItemMultiplier(BuyMultiplier: 1.10f, SellMultiplier: 0.99f), // Blueberry Seeds
            ["482"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.56f), // Pepper Seeds (Hot Pepper)
            ["479"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.06f), // Melon Seeds
            ["480"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.70f), // Tomato Seeds
            ["487"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 2.41f), // Corn Seeds
            ["302"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.26f), // Hops Starter
            ["483"] = new CropItemMultiplier(BuyMultiplier: 0.85f, SellMultiplier: 2.14f), // Wheat Seeds
            ["484"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.36f), // Radish Seeds
            ["485"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.74f), // Red Cabbage Seeds
            ["486"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.00f), // Starfruit Seeds

            // Fall
            ["493"] = new CropItemMultiplier(BuyMultiplier: 1.12f, SellMultiplier: 0.99f), // Cranberry Seeds
            ["488"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.59f), // Eggplant Seeds
            ["301"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.11f), // Grape Starter
            ["299"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.91f), // Amaranth Seeds
            ["494"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.17f), // Beet Seeds
            ["492"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 1.54f), // Yam Seeds
            ["490"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.90f), // Pumpkin Seeds
            ["489"] = new CropItemMultiplier(BuyMultiplier: 0.95f, SellMultiplier: 1.00f), // Artichoke Seeds
            ["491"] = new CropItemMultiplier(BuyMultiplier: 1.00f, SellMultiplier: 0.94f), // Bok Choy Seeds
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
