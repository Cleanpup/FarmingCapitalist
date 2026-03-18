using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Animal-product-specific pricing rules.
    /// Direct raw outputs stay in this category; processed artisan goods are excluded upstream.
    /// </summary>
    internal static class AnimalProductTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            AnimalProductEconomicTrait traits = AnimalProductTraitService.GetTraits(item);
            if (traits == AnimalProductEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalTrufflePriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetSellModifierForTraits(traits);
            return modifier;
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            AnimalProductEconomicTrait traits = AnimalProductTraitService.GetTraits(item);
            if (traits == AnimalProductEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSeasonalTrufflePriceModifier(item, context);
            modifier *= SaveEconomyProfileService.GetBuyModifierForTraits(traits);
            return modifier;
        }

        private static float GetSeasonalTrufflePriceModifier(Item item, EconomyContext context)
        {
            if (!AnimalProductTraitService.IsSeasonalTruffle(item))
                return 1f;

            return NormalizeSeasonKey(context.Season) switch
            {
                "winter" => AnimalProductMarketTuning.TruffleWinterPriceMultiplier,
                "summer" => AnimalProductMarketTuning.TruffleSummerPriceMultiplier,
                "fall" => AnimalProductMarketTuning.TruffleFallPriceMultiplier,
                _ => AnimalProductMarketTuning.TruffleSpringPriceMultiplier
            };
        }

        private static string NormalizeSeasonKey(string? seasonKey)
        {
            return string.IsNullOrWhiteSpace(seasonKey)
                ? string.Empty
                : seasonKey.Trim().ToLowerInvariant();
        }
    }
}
