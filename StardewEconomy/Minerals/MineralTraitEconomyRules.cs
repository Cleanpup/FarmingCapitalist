using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Broad mineral balancing by mineral rarity and value traits.
    /// </summary>
    internal static class MineralTraitEconomyRules
    {
        private const float CommonSellMultiplier = 1f;
        private const float UncommonSellMultiplier = 1f;
        private const float RareSellMultiplier = 1f;
        private const float LuxurySellMultiplier = 1f;

        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            MineralEconomicTrait traits = MineralTraitService.GetTraits(item);
            if (traits == MineralEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSellTraitMultiplier(traits, MineralEconomicTrait.Common, CommonSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, MineralEconomicTrait.Uncommon, UncommonSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, MineralEconomicTrait.Rare, RareSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, MineralEconomicTrait.Luxury, LuxurySellMultiplier);
            modifier *= SaveEconomyProfileService.GetSellModifierForTraits(traits);
            return modifier;
        }

        private static float GetSellTraitMultiplier(MineralEconomicTrait allTraits, MineralEconomicTrait trait, float multiplier)
        {
            return (allTraits & trait) == trait
                ? multiplier
                : 1f;
        }
    }
}
