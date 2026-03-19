using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Cooking-food-specific pricing rules.
    /// This category is year-round, so only profile-driven trait multipliers apply.
    /// </summary>
    internal static class CookingFoodTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CookingFoodEconomicTrait traits = CookingFoodTraitService.GetTraits(item);
            if (traits == CookingFoodEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CookingFoodEconomicTrait traits = CookingFoodTraitService.GetTraits(item);
            if (traits == CookingFoodEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetBuyModifierForTraits(traits);
        }
    }
}
