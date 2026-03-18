using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Forageable-specific pricing rules.
    /// Seasonal pressure only applies to seasonal forage; year-round forage stays neutral.
    /// </summary>
    internal static class ForageableTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            ForageableEconomicTrait traits = ForageableTraitService.GetTraits(item);
            if (traits == ForageableEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalForagePriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetSellModifierForTraits(traits);
            return modifier;
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            ForageableEconomicTrait traits = ForageableTraitService.GetTraits(item);
            if (traits == ForageableEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalForagePriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetBuyModifierForTraits(traits);
            return modifier;
        }

        private static float GetSeasonalForagePriceModifier(Item item, EconomyContext context)
        {
            if (!ForageableTraitService.HasTrait(item, ForageableEconomicTrait.SeasonalForage))
                return ForageableMarketTuning.YearRoundPriceMultiplier;

            string normalizedSeasonKey = NormalizeSeasonKey(context.Season);
            if (string.IsNullOrWhiteSpace(normalizedSeasonKey))
                return 1f;

            IReadOnlyCollection<string> availableSeasons = ForageableTraitService.GetAvailableSeasonKeys(item);
            if (availableSeasons.Count == 0 || availableSeasons.Count >= 4)
                return ForageableMarketTuning.YearRoundPriceMultiplier;

            return availableSeasons.Contains(normalizedSeasonKey, StringComparer.OrdinalIgnoreCase)
                ? ForageableMarketTuning.InSeasonPriceMultiplier
                : ForageableMarketTuning.OutOfSeasonPriceMultiplier;
        }

        private static string NormalizeSeasonKey(string? seasonKey)
        {
            return string.IsNullOrWhiteSpace(seasonKey)
                ? string.Empty
                : seasonKey.Trim().ToLowerInvariant();
        }
    }
}
