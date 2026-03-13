using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

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
            float totalModifier = 1f;
            float festivalModifier = FestivalEconomyRules.GetFestivalSellModifier(item, context);
            float categoryModifier = CategoryEconomyRules.GetSellCategoryModifier(item, context);
            float cropTraitModifier = CropTraitEconomyRules.GetSellTraitModifier(item, context);
            float cropItemModifier = CropItemEconomyRules.GetSellItemModifier(item, context);
            float cropSupplyModifier = 1f;
            bool applySupplyModifier = CropSupplyModifierService.ApplyToLiveSellPricing
                || CropSupplyModifierService.HasDebugSellModifierOverride;

            totalModifier *= festivalModifier;
            totalModifier *= categoryModifier;
            totalModifier *= cropTraitModifier;
            totalModifier *= cropItemModifier;
            if (applySupplyModifier)
            {
                cropSupplyModifier = CropSupplyModifierService.GetSellModifier(item);
                totalModifier *= cropSupplyModifier;
            }

            int adjusted = Math.Max(0, (int)Math.Round(vanillaPrice * totalModifier, MidpointRounding.AwayFromZero));

            string supplyLabel = CropSupplyModifierService.HasDebugSellModifierOverride
                ? "cropSupplyOverride"
                : "cropSupply";
            string supplyTrace = applySupplyModifier
                ? $", {supplyLabel} x{cropSupplyModifier:0.###}"
                : string.Empty;

            VerbosePriceTraceLogger.Log(
                $"Sell price modifiers: festival x{festivalModifier:0.###}, category x{categoryModifier:0.###}, cropTrait x{cropTraitModifier:0.###}, cropItem x{cropItemModifier:0.###}{supplyTrace} -> total x{totalModifier:0.###}"
            );

            VerbosePriceTraceLogger.Log(
                $"AdjustSellPrice: {vanillaPrice} -> {adjusted} (festival: {context.FestivalTomorrowName ?? "none"})"
            );

            return ClampStoreSellBackPrice(adjusted, item);
        }

        // Buy price adjustment with friendship/day/festival/category plus bulk-buy ramp.
        public static int AdjustBuyPrice(
            int vanillaPrice,
            ISalable item,
            string shopId,
            EconomyContext context,
            int cumulativePurchasedToday = 0,
            int purchaseQuantity = 1
        )
        {
            float totalModifier = 1f;

            float friendshipMultiplier = RelationshipModifier(Math.Clamp((float)context.HeartsWithShopkeeper, 0f, 10f), shopId); // ( hearts,shopid)
            float dayMultiplier = DayModifier(context.DayOfMonth, item);
            float festivalMultiplier = FestivalEconomyRules.GetFestivalBuyModifier(item, context); // handled in separate class
            float categoryMultiplier = CategoryEconomyRules.GetBuyCategoryModifier(item, shopId, context);
            float cropTraitMultiplier = 1f;
            float cropItemMultiplier = 1f;
            if (item is Item asItem)
            {
                cropTraitMultiplier = CropTraitEconomyRules.GetBuyTraitModifier(asItem, context);
                cropItemMultiplier = CropItemEconomyRules.GetBuyItemModifier(asItem, context);
            }
            float bulkRampMultiplier = BulkBuyRampRules.GetMultiplier(
                item,
                shopId,
                cumulativePurchasedToday,
                purchaseQuantity
            );

            totalModifier *= dayMultiplier;
            totalModifier *= friendshipMultiplier;
            totalModifier *= festivalMultiplier;
            totalModifier *= categoryMultiplier;
            totalModifier *= cropTraitMultiplier;
            totalModifier *= cropItemMultiplier;
            totalModifier *= bulkRampMultiplier;
            VerbosePriceTraceLogger.Log(
                $"Buy price modifiers for shop {shopId}: day x{dayMultiplier:0.###}, friendship x{friendshipMultiplier:0.###}, festival x{festivalMultiplier:0.###}, category x{categoryMultiplier:0.###}, cropTrait x{cropTraitMultiplier:0.###}, cropItem x{cropItemMultiplier:0.###}, bulk x{bulkRampMultiplier:0.###} (daily {cumulativePurchasedToday}, qty {purchaseQuantity}) -> total x{totalModifier:0.###}"
            );

            int adjusted = Math.Max(1, (int)Math.Round(vanillaPrice * totalModifier, MidpointRounding.AwayFromZero));
            

            VerbosePriceTraceLogger.Log(
                $"AdjustBuyPrice: {vanillaPrice} -> {adjusted} (hearts: {context.HeartsWithShopkeeper}, festival: {context.FestivalTomorrowName ?? "none"}) -> (total x{totalModifier:0.###})"
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
        public static float DayModifier(int dayOfMonth, ISalable item)
        {
            int day = Math.Clamp(dayOfMonth, 1, 28);

            if (day >= 25 && item is Item stardewItem && ItemCategoryRules.IsSeed(stardewItem))
            {
                float maxDiscount = 0.30f;      // big clearance
                float t = (day - 25f) / 3f;     // 0..1
                return 1f - t * maxDiscount;
            }
            else if (day > 15 && item is Item stardewItem2 && ItemCategoryRules.IsSeed(stardewItem2))
            {
                float maxDiscount = 0.10f;      // gentle mid-season discount
                float t = (day - 15f) / 10f;    // day 16..25 -> 0.1..1
                return 1f - t * maxDiscount;    // down to 0.90 by day 25
            }

            return 1f;
        }

        private static int ClampStoreSellBackPrice(int calculatedSellPrice, Item item)
        {
            if (calculatedSellPrice <= 0)
                return calculatedSellPrice;

            if (Game1.activeClickableMenu is not ShopMenu shopMenu || shopMenu.currency != 0)
                return calculatedSellPrice;

            if (!ShopPriceRuntimeService.TryGetCurrentAdjustedBuyPrice(shopMenu, item, out int adjustedBuyPrice))
                return calculatedSellPrice;

            int clamped = Math.Min(calculatedSellPrice, adjustedBuyPrice);
            if (clamped < calculatedSellPrice)
            {
                Monitor?.Log(
                    $"Sell-back clamp applied in shop {shopMenu.ShopId ?? "<unknown>"} for {item.Name} ({item.QualifiedItemId}): sell {calculatedSellPrice} -> {clamped}, current buy {adjustedBuyPrice}.",
                    LogLevel.Trace
                );
            }

            return clamped;
        }
    }
}
