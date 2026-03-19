using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Equipment-specific pricing rules.
    /// This category is year-round, so only profile-driven trait multipliers apply.
    /// </summary>
    internal static class EquipmentTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            EquipmentEconomicTrait traits = EquipmentTraitService.GetTraits(item);
            if (traits == EquipmentEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            EquipmentEconomicTrait traits = EquipmentTraitService.GetTraits(item);
            if (traits == EquipmentEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetBuyModifierForTraits(traits);
        }
    }
}
