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
        public static int AdjustSellPrice(int vanillaPrice, Item item, EconomyContext context) // cleanup switch cases
        {
            float totalModifier = 1f;
            float festivalModifier = FestivalEconomyRules.GetFestivalSellModifier(item, context);
            float categoryModifier = CategoryEconomyRules.GetSellCategoryModifier(item, context);
            float cropTraitModifier = CropTraitEconomyRules.GetSellTraitModifier(item, context);
            float cropItemModifier = CropItemEconomyRules.GetSellItemModifier(item, context);
            float fishTraitModifier = FishTraitEconomyRules.GetSellTraitModifier(item, context);
            float mineralTraitModifier = MineralTraitEconomyRules.GetSellTraitModifier(item, context);
            float animalProductTraitModifier = AnimalProductTraitEconomyRules.GetSellTraitModifier(item, context);
            float forageableTraitModifier = ForageableTraitEconomyRules.GetSellTraitModifier(item, context);
            float artisanGoodTraitModifier = ArtisanGoodTraitEconomyRules.GetSellTraitModifier(item, context);
            float monsterLootTraitModifier = MonsterLootTraitEconomyRules.GetSellTraitModifier(item, context);
            float cropSupplyModifier = 1f;
            float fishSupplyModifier = 1f;
            float mineralSupplyModifier = 1f;
            float animalProductSupplyModifier = 1f;
            float forageableSupplyModifier = 1f;
            float artisanGoodSupplyModifier = 1f;
            float monsterLootSupplyModifier = 1f;
            bool applyCropSupplyModifier = CropSupplyModifierService.ApplyToLiveSellPricing
                || CropSupplyModifierService.HasDebugSellModifierOverride;
            bool fishSupplySystemEnabled = FishSupplyModifierService.ApplyToLiveSellPricing
                || FishSupplyModifierService.HasDebugSellModifierOverride;
            bool mineralSupplySystemEnabled = MineralSupplyModifierService.ApplyToLiveSellPricing
                || MineralSupplyModifierService.HasDebugSellModifierOverride;
            bool animalProductSupplySystemEnabled = AnimalProductSupplyModifierService.ApplyToLiveSellPricing
                || AnimalProductSupplyModifierService.HasDebugSellModifierOverride;
            bool forageableSupplySystemEnabled = ForageableSupplyModifierService.ApplyToLiveSellPricing
                || ForageableSupplyModifierService.HasDebugSellModifierOverride;
            bool artisanGoodSupplySystemEnabled = ArtisanGoodSupplyModifierService.ApplyToLiveSellPricing
                || ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride;
            bool monsterLootSupplySystemEnabled = MonsterLootSupplyModifierService.ApplyToLiveSellPricing
                || MonsterLootSupplyModifierService.HasDebugSellModifierOverride;
            bool applyFishSupplyModifier = false;
            bool applyMineralSupplyModifier = false;
            bool applyAnimalProductSupplyModifier = false;
            bool applyForageableSupplyModifier = false;
            bool applyArtisanGoodSupplyModifier = false;
            bool applyMonsterLootSupplyModifier = false;

            if (fishSupplySystemEnabled
                && FishEconomyItemRules.TryGetFishEconomyClassification(
                    item,
                    out _,
                    out bool isFishEconomyEligible,
                    logDecision: true,
                    context: "fish-sell"
                ))
            {
                applyFishSupplyModifier = isFishEconomyEligible;
            }

            if (mineralSupplySystemEnabled && MineralEconomyItemRules.IsMineralEconomyEligible(item))
                applyMineralSupplyModifier = true;

            if (animalProductSupplySystemEnabled && AnimalProductEconomyItemRules.IsAnimalProductEligible(item))
                applyAnimalProductSupplyModifier = true;

            if (forageableSupplySystemEnabled && ForageableEconomyItemRules.IsForageableEligible(item))
                applyForageableSupplyModifier = true;

            if (artisanGoodSupplySystemEnabled && ArtisanGoodEconomyItemRules.IsArtisanGoodEligible(item))
                applyArtisanGoodSupplyModifier = true;

            if (monsterLootSupplySystemEnabled && MonsterLootEconomyItemRules.IsMonsterLootEligible(item))
                applyMonsterLootSupplyModifier = true;

            totalModifier *= festivalModifier;
            totalModifier *= categoryModifier;
            totalModifier *= cropTraitModifier;
            totalModifier *= cropItemModifier;
            totalModifier *= fishTraitModifier;
            totalModifier *= mineralTraitModifier;
            totalModifier *= animalProductTraitModifier;
            totalModifier *= forageableTraitModifier;
            totalModifier *= artisanGoodTraitModifier;
            totalModifier *= monsterLootTraitModifier;
            if (applyCropSupplyModifier)
            {
                cropSupplyModifier = CropSupplyModifierService.GetSellModifier(item);
                totalModifier *= cropSupplyModifier;
            }

            if (applyFishSupplyModifier)
            {
                fishSupplyModifier = FishSupplyModifierService.GetSellModifier(item);
                totalModifier *= fishSupplyModifier;
            }

            if (applyMineralSupplyModifier)
            {
                mineralSupplyModifier = MineralSupplyModifierService.GetSellModifier(item);
                totalModifier *= mineralSupplyModifier;
            }

            if (applyAnimalProductSupplyModifier)
            {
                animalProductSupplyModifier = AnimalProductSupplyModifierService.GetSellModifier(item);
                totalModifier *= animalProductSupplyModifier;
            }

            if (applyForageableSupplyModifier)
            {
                forageableSupplyModifier = ForageableSupplyModifierService.GetSellModifier(item);
                totalModifier *= forageableSupplyModifier;
            }

            if (applyArtisanGoodSupplyModifier)
            {
                artisanGoodSupplyModifier = ArtisanGoodSupplyModifierService.GetSellModifier(item);
                totalModifier *= artisanGoodSupplyModifier;
            }

            if (applyMonsterLootSupplyModifier)
            {
                monsterLootSupplyModifier = MonsterLootSupplyModifierService.GetSellModifier(item);
                totalModifier *= monsterLootSupplyModifier;
            }

            int adjustedBeforeClamp = Math.Max(0, (int)Math.Round(vanillaPrice * totalModifier, MidpointRounding.AwayFromZero));

            string supplyLabel = CropSupplyModifierService.HasDebugSellModifierOverride
                ? "cropSupplyOverride"
                : "cropSupply";
            string supplyTrace = applyCropSupplyModifier
                ? $", {supplyLabel} x{cropSupplyModifier:0.###}"
                : string.Empty;
            string fishSupplyTrace = applyFishSupplyModifier
                ? $", {(FishSupplyModifierService.HasDebugSellModifierOverride ? "fishSupplyOverride" : "fishSupply")} x{fishSupplyModifier:0.###}"
                : string.Empty;
            string mineralSupplyTrace = applyMineralSupplyModifier
                ? $", {(MineralSupplyModifierService.HasDebugSellModifierOverride ? "miningSupplyOverride" : "miningSupply")} x{mineralSupplyModifier:0.###}"
                : string.Empty;
            string animalProductSupplyTrace = applyAnimalProductSupplyModifier
                ? $", {(AnimalProductSupplyModifierService.HasDebugSellModifierOverride ? "animalProductSupplyOverride" : "animalProductSupply")} x{animalProductSupplyModifier:0.###}"
                : string.Empty;
            string forageableSupplyTrace = applyForageableSupplyModifier
                ? $", {(ForageableSupplyModifierService.HasDebugSellModifierOverride ? "forageableSupplyOverride" : "forageableSupply")} x{forageableSupplyModifier:0.###}"
                : string.Empty;
            string artisanGoodSupplyTrace = applyArtisanGoodSupplyModifier
                ? $", {(ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride ? "artisanGoodSupplyOverride" : "artisanGoodSupply")} x{artisanGoodSupplyModifier:0.###}"
                : string.Empty;
            string monsterLootSupplyTrace = applyMonsterLootSupplyModifier
                ? $", {(MonsterLootSupplyModifierService.HasDebugSellModifierOverride ? "monsterLootSupplyOverride" : "monsterLootSupply")} x{monsterLootSupplyModifier:0.###}"
                : string.Empty;

            VerbosePriceTraceLogger.Log(
                $"Sell price modifiers: festival x{festivalModifier:0.###}, category x{categoryModifier:0.###}, cropTrait x{cropTraitModifier:0.###}, cropItem x{cropItemModifier:0.###}, fishTrait x{fishTraitModifier:0.###}, miningTrait x{mineralTraitModifier:0.###}, animalProductTrait x{animalProductTraitModifier:0.###}, forageableTrait x{forageableTraitModifier:0.###}, artisanGoodTrait x{artisanGoodTraitModifier:0.###}, monsterLootTrait x{monsterLootTraitModifier:0.###}{supplyTrace}{fishSupplyTrace}{mineralSupplyTrace}{animalProductSupplyTrace}{forageableSupplyTrace}{artisanGoodSupplyTrace}{monsterLootSupplyTrace} -> total x{totalModifier:0.###}"
            );

            VerbosePriceTraceLogger.Log(
                $"AdjustSellPrice: {vanillaPrice} -> {adjustedBeforeClamp} (festival: {context.FestivalTomorrowName ?? "none"})"
            );

            return ClampStoreSellBackPrice(adjustedBeforeClamp, item);
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
            float animalProductTraitMultiplier = 1f;
            float forageableTraitMultiplier = 1f;
            float artisanGoodTraitMultiplier = 1f;
            float monsterLootTraitMultiplier = 1f;
            if (item is Item asItem)
            {
                cropTraitMultiplier = CropTraitEconomyRules.GetBuyTraitModifier(asItem, context);
                cropItemMultiplier = CropItemEconomyRules.GetBuyItemModifier(asItem, context);
                animalProductTraitMultiplier = AnimalProductTraitEconomyRules.GetBuyTraitModifier(asItem, context);
                forageableTraitMultiplier = ForageableTraitEconomyRules.GetBuyTraitModifier(asItem, context);
                artisanGoodTraitMultiplier = ArtisanGoodTraitEconomyRules.GetBuyTraitModifier(asItem, context);
                monsterLootTraitMultiplier = MonsterLootTraitEconomyRules.GetBuyTraitModifier(asItem, context);
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
            totalModifier *= animalProductTraitMultiplier;
            totalModifier *= forageableTraitMultiplier;
            totalModifier *= artisanGoodTraitMultiplier;
            totalModifier *= monsterLootTraitMultiplier;
            totalModifier *= bulkRampMultiplier;
            VerbosePriceTraceLogger.Log(
                $"Buy price modifiers for shop {shopId}: day x{dayMultiplier:0.###}, friendship x{friendshipMultiplier:0.###}, festival x{festivalMultiplier:0.###}, category x{categoryMultiplier:0.###}, cropTrait x{cropTraitMultiplier:0.###}, cropItem x{cropItemMultiplier:0.###}, animalProductTrait x{animalProductTraitMultiplier:0.###}, forageableTrait x{forageableTraitMultiplier:0.###}, artisanGoodTrait x{artisanGoodTraitMultiplier:0.###}, monsterLootTrait x{monsterLootTraitMultiplier:0.###}, bulk x{bulkRampMultiplier:0.###} (daily {cumulativePurchasedToday}, qty {purchaseQuantity}) -> total x{totalModifier:0.###}"
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
            bool isSellingSeedsLateSeason = day >= 25 && item is Item stardewItem && ItemCategoryRules.IsSeed(stardewItem);
            bool isSellingSeedsMidSeason = day > 15 && item is Item stardewItem1 && ItemCategoryRules.IsSeed(stardewItem1);

            if (isSellingSeedsLateSeason)
            {
                float maxDiscount = 0.30f;      // big clearance
                float t = (day - 25f) / 3f;     // 0..1
                return 1f - t * maxDiscount;
            }
            else if (isSellingSeedsMidSeason)
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
            {
                Monitor?.Log(
                    $"Sell-back clamp skipped in shop {shopMenu.ShopId ?? "<unknown>"} for {item.Name} ({item.QualifiedItemId}): no canonical buy price matched the current item.",
                    LogLevel.Trace
                );
                return calculatedSellPrice;
            }

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
