using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Category-driven price adjustments independent from festival logic.
    /// </summary>
        internal class CategoryEconomyRules
    {
        public static float GetBuyCategoryModifier(ISalable item, string shopId, EconomyContext context)
        {
            _ = shopId;
            _ = context;

            if (item is not Item asItem)
                return 1f;

            if (ItemCategoryRules.IsSeed(asItem))
                return 1.02f;

            if (ItemCategoryRules.IsArtisanGood(asItem))
                return 1.03f;

            if (ItemCategoryRules.IsFish(asItem))
                return 1.01f;

            return 1f;
        }

        public static float GetSellCategoryModifier(Item item, EconomyContext context)
        {
            _ = context;

            if (ItemCategoryRules.IsFish(item))
                return 1.02f;

            if (ItemCategoryRules.IsGem(item) || ItemCategoryRules.IsMineral(item))
                return 1.03f;

            if (ItemCategoryRules.IsArtisanGood(item))
                return 1.02f;

            return 1f;
        }
    }
}
