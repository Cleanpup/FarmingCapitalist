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

        // Sell price adjustment: festival-driven demand shifts.
        public static int AdjustSellPrice(int vanillaPrice, Item item, EconomyContext context)
        {
            float totalModifier = FestivalEconomyRules.GetFestivalSellModifier(item, context);
            int adjusted = Math.Max(0, (int)Math.Round(vanillaPrice * totalModifier, MidpointRounding.AwayFromZero));

            Monitor?.Log(
                $"AdjustSellPrice: {vanillaPrice} -> {adjusted} (festival: {context.FestivalTomorrowName ?? "none"}) -> (total x{totalModifier:0.###})",
                LogLevel.Trace
            );

            return adjusted;
        }

        // Buy price adjustment: temporary friendship-only discount foundation.
        public static int AdjustBuyPrice(int vanillaPrice, ISalable item, string shopId, EconomyContext context)
        {
            float totalModifier = 1f;

            float friendshipMultiplier = RelationshipModifier(Math.Clamp((float)context.HeartsWithShopkeeper, 0f, 10f), shopId); // ( hearts,shopid)
            float dayMultiplier = DayModifier(context.DayOfMonth);
            float festivalMultiplier = FestivalEconomyRules.GetFestivalBuyModifier(item, context);

            totalModifier *= dayMultiplier;
            totalModifier *= friendshipMultiplier;
            totalModifier *= festivalMultiplier;
            Monitor?.Log(
                $"Buy price modifiers for shop {shopId}: day x{dayMultiplier:0.###}, friendship x{friendshipMultiplier:0.###}, festival x{festivalMultiplier:0.###} -> total x{totalModifier:0.###}",
                LogLevel.Trace
            );

            int adjusted = Math.Max(1, (int)Math.Round(vanillaPrice * totalModifier, MidpointRounding.AwayFromZero));
            

            Monitor?.Log(
                $"AdjustBuyPrice: {vanillaPrice} -> {adjusted} (hearts: {context.HeartsWithShopkeeper}, festival: {context.FestivalTomorrowName ?? "none"}) -> (total x{totalModifier:0.###})",
                LogLevel.Trace
            );

            return adjusted;
        }

        public static float RelationshipModifier(float hearts, string shopId)
        {
            if (shopId == "Joja")
                return 1f;
            float friendshipMultiplier;

            if (hearts < 5f)
            {
                float maxMarkup = 0.10f;
                friendshipMultiplier = 1f + ((5f - hearts) / 5f) * maxMarkup;
            }
            else
            {
                float maxDiscount = 0.15f;
                friendshipMultiplier = 1f - ((hearts - 5f) / 5f) * maxDiscount;
            }

            return(friendshipMultiplier);
        }
        public static float DayModifier(int dayOfMonth)
        {
            float day = Math.Clamp(dayOfMonth, 1, 28);

            if (day >= 25f)
            {
                float maxDiscount = 0.30f;      // big clearance
                float t = (day - 25f) / 3f;     // 0..1
                return 1f - t * maxDiscount;
            }
            else if (day > 15f)
            {
                float maxDiscount = 0.10f;      // gentle mid-season discount
                float t = (day - 15f) / 10f;    // day 16..25 -> 0.1..1
                return 1f - t * maxDiscount;    // down to 0.90 by day 25
            }

            return 1f;
        }
    }
}
