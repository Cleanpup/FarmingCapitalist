using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Artisan-good-specific pricing rules.
    /// This category is year-round, so only profile-driven trait multipliers apply.
    /// </summary>
    internal static class ArtisanGoodTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            ArtisanGoodEconomicTrait traits = ArtisanGoodTraitService.GetTraits(item);
            if (traits == ArtisanGoodEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            ArtisanGoodEconomicTrait traits = ArtisanGoodTraitService.GetTraits(item);
            if (traits == ArtisanGoodEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetBuyModifierForTraits(traits);
        }
    }
}
