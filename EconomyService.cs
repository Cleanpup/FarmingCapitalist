using System;
using StardewModdingAPI;
using StardewValley;

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

        // Buy price adjustment: temporary friendship-only discount foundation.
        public static int AdjustBuyPrice(int vanillaPrice, ISalable item, string shopId, EconomyContext context)
        {
            float maxDiscount = 0.15f;
            float friendshipMultiplier = 1f - (context.HeartsWithShopkeeper / 10f) * maxDiscount;

            int adjusted = Math.Max(1, (int)(vanillaPrice * friendshipMultiplier));

            Monitor?.Log(
                $"AdjustBuyPrice: {vanillaPrice} -> {adjusted} (hearts: {context.HeartsWithShopkeeper})",
                LogLevel.Trace
            );

            return adjusted;
        }
    }
}
