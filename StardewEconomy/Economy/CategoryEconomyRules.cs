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
                return 1.00f;

            if (ItemCategoryRules.IsArtisanGood(asItem))
                return 1.00f;

            if (ArtisanGoodEconomyItemRules.IsArtisanGoodEligible(asItem))
                return 1.00f;

            if (FishEconomyItemRules.IsFishEconomyEligible(asItem))
                return 1.00f;

            if (AnimalProductEconomyItemRules.IsAnimalProductEligible(asItem))
                return 1.00f;

            if (ForageableEconomyItemRules.IsForageableEligible(asItem))
                return 1.00f;

            if (MonsterLootEconomyItemRules.IsMonsterLootEligible(asItem))
                return 1.00f;

            return 1f;
        }

        public static float GetSellCategoryModifier(Item item, EconomyContext context)
        {
            _ = context;

            if (FishEconomyItemRules.IsFishEconomyEligible(item))
                return 1.00f;

            if (MineralEconomyItemRules.IsMineralEconomyEligible(item))
                return 1.00f;

            if (AnimalProductEconomyItemRules.IsAnimalProductEligible(item))
                return 1.00f;

            if (ForageableEconomyItemRules.IsForageableEligible(item))
                return 1.00f;

            if (ArtisanGoodEconomyItemRules.IsArtisanGoodEligible(item))
                return 1.00f;

            if (MonsterLootEconomyItemRules.IsMonsterLootEligible(item))
                return 1.00f;

            if (ItemCategoryRules.IsArtisanGood(item))
                return 1.00f;

            return 1f;
        }
    }
}
