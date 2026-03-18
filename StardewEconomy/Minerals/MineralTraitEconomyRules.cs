using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Broad mining balancing by mining traits.
    /// </summary>
    internal static class MineralTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            MineralEconomicTrait traits = MineralTraitService.GetTraits(item);
            if (traits == MineralEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }
    }
}
