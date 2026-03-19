using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Crafting-extra-specific pricing rules.
    /// This category is year-round and intentionally has no randomized subcategory selection, so profile participation stays neutral unless populated later.
    /// </summary>
    internal static class CraftingExtraTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CraftingExtraEconomicTrait traits = CraftingExtraTraitService.GetTraits(item);
            if (traits == CraftingExtraEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CraftingExtraEconomicTrait traits = CraftingExtraTraitService.GetTraits(item);
            if (traits == CraftingExtraEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetBuyModifierForTraits(traits);
        }
    }
}
