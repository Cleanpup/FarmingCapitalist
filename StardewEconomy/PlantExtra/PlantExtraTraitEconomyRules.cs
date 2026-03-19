using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Plant-extra-specific pricing rules.
    /// Tree fruit and flowers use season-aware pricing, seeds stay neutral, and mushrooms rise only in winter.
    /// </summary>
    internal static class PlantExtraTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            PlantExtraEconomicTrait traits = PlantExtraTraitService.GetTraits(item);
            if (traits == PlantExtraEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalPriceModifier(item, context);
            modifier *= GetWinterMushroomPriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetSellModifierForTraits(traits);
            return modifier;
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            PlantExtraEconomicTrait traits = PlantExtraTraitService.GetTraits(item);
            if (traits == PlantExtraEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalPriceModifier(item, context);
            modifier *= GetWinterMushroomPriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetBuyModifierForTraits(traits);
            return modifier;
        }

        private static float GetSeasonalPriceModifier(Item item, EconomyContext context)
        {
            bool isSeasonalPlantGood =
                PlantExtraTraitService.HasTrait(item, PlantExtraEconomicTrait.TreeFruit)
                || PlantExtraTraitService.HasTrait(item, PlantExtraEconomicTrait.Flower);
            if (!isSeasonalPlantGood)
                return PlantExtraMarketTuning.YearRoundPriceMultiplier;

            string normalizedSeasonKey = NormalizeSeasonKey(context.Season);
            if (string.IsNullOrWhiteSpace(normalizedSeasonKey))
                return 1f;

            IReadOnlyCollection<string> availableSeasons = PlantExtraTraitService.GetAvailableSeasonKeys(item);
            if (availableSeasons.Count == 0 || availableSeasons.Count >= 4)
                return PlantExtraMarketTuning.YearRoundPriceMultiplier;

            return availableSeasons.Contains(normalizedSeasonKey, StringComparer.OrdinalIgnoreCase)
                ? PlantExtraMarketTuning.InSeasonPriceMultiplier
                : PlantExtraMarketTuning.OutOfSeasonPriceMultiplier;
        }

        private static float GetWinterMushroomPriceModifier(Item item, EconomyContext context)
        {
            if (!PlantExtraTraitService.HasTrait(item, PlantExtraEconomicTrait.Mushroom))
                return 1f;

            string normalizedSeasonKey = NormalizeSeasonKey(context.Season);
            return string.Equals(normalizedSeasonKey, "winter", StringComparison.OrdinalIgnoreCase)
                ? PlantExtraMarketTuning.WinterMushroomPriceMultiplier
                : 1f;
        }

        private static string NormalizeSeasonKey(string? seasonKey)
        {
            return string.IsNullOrWhiteSpace(seasonKey)
                ? string.Empty
                : seasonKey.Trim().ToLowerInvariant();
        }
    }
}
