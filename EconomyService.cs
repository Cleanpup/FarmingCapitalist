using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Business logic for pricing adjustments. Kept separate from Harmony glue
    /// so the patch methods remain minimal and testable.
    /// </summary>
    internal static class EconomyService
    {
        internal static IMonitor? Monitor;

        // Test behaviour: double the sell price
        public static int AdjustSellPrice(int vanillaPrice)
        {
            Monitor?.Log($"AdjustSellPrice: {vanillaPrice} -> {vanillaPrice * 2}", LogLevel.Trace);
            return vanillaPrice * 2;
        }

        // Test behaviour: double the buy price
        public static int AdjustBuyPrice(int vanillaPrice)
        {
            Monitor?.Log($"AdjustBuyPrice: {vanillaPrice} -> {vanillaPrice * 2}", LogLevel.Trace);
            return vanillaPrice * 2;
        }
    }
}
