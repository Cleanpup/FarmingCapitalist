using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Broad fish balancing by fish behavior traits.
    /// Mirrors crop trait profile application while staying fish-specific.
    /// </summary>
    internal static class FishTraitEconomyRules
    {
        private const float SpringSellMultiplier = 1f;
        private const float SummerSellMultiplier = 1f;
        private const float FallSellMultiplier = 1f;
        private const float WinterSellMultiplier = 1f;

        private const float MorningSellMultiplier = 1f;
        private const float DaySellMultiplier = 1f;
        private const float EveningSellMultiplier = 1f;
        private const float NightSellMultiplier = 1f;

        private const float SunnySellMultiplier = 1f;
        private const float RainySellMultiplier = 1f;

        private const float TrapSellMultiplier = 1f;
        private const float LineCaughtSellMultiplier = 1f;

        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            FishEconomicTrait traits = FishTraitService.GetTraits(item);
            if (traits == FishEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Spring, SpringSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Summer, SummerSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Fall, FallSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Winter, WinterSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Morning, MorningSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Day, DaySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Evening, EveningSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Night, NightSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Sunny, SunnySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Rainy, RainySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.Trap, TrapSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, FishEconomicTrait.LineCaught, LineCaughtSellMultiplier);
            modifier *= SaveEconomyProfileService.GetSellModifierForTraits(traits);
            return modifier;
        }

        private static float GetSellTraitMultiplier(FishEconomicTrait allTraits, FishEconomicTrait trait, float multiplier)
        {
            return (allTraits & trait) == trait
                ? multiplier
                : 1f;
        }
    }
}
