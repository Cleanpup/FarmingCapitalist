using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Monster-loot-specific pricing rules.
    /// This category is year-round, so only profile-driven trait multipliers apply once the profile layer is wired.
    /// </summary>
    internal static class MonsterLootTraitEconomyRules
    {
        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            MonsterLootEconomicTrait traits = MonsterLootTraitService.GetTraits(item);
            if (traits == MonsterLootEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetSellModifierForTraits(traits);
        }

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            MonsterLootEconomicTrait traits = MonsterLootTraitService.GetTraits(item);
            if (traits == MonsterLootEconomicTrait.None)
                return 1f;

            return SaveEconomyProfileService.GetBuyModifierForTraits(traits);
        }
    }
}
