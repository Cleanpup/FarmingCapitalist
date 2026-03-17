using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared category contract for trait-based sell pricing.
    /// </summary>
    internal interface ICategoryTraitEconomyRules
    {
        /// <summary>Get the sell-price modifier contributed by the item's category traits.</summary>
        float GetSellTraitModifier(Item item, EconomyContext context);
    }
}
