using System;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Festival-specific demand adjustments for buying and selling.
    /// </summary>
    internal static class FestivalEconomyRules
    {
        public static float GetFestivalBuyModifier(ISalable item, EconomyContext context)
        {
            if (!context.FestivalTomorrow || string.IsNullOrWhiteSpace(context.FestivalTomorrowName))
                return 1f;

            if (item is not Item asItem)
                return 1f;

            string festivalName = context.FestivalTomorrowName!;

            if (IsFestival(festivalName, "Egg Festival"))
            {
                if (ItemCategoryRules.IsEgg(asItem))
                    return 1.15f;
            }

            return 1f;
        }

        public static float GetFestivalSellModifier(Item item, EconomyContext context)
        {
            if (!context.FestivalTomorrow || string.IsNullOrWhiteSpace(context.FestivalTomorrowName))
                return 1f;

            string festivalName = context.FestivalTomorrowName!;
            if (IsFestival(festivalName, "Egg Festival"))
            {
                if (ItemCategoryRules.IsEgg(item))
                    return 1.15f;
            }

            if (IsFestival(festivalName, "Flower Dance"))
            {
                if (ItemCategoryRules.IsFlower(item))
                    return 1.20f;
            }
            else if (IsFestival(festivalName, "Luau"))
            {
                if (ItemCategoryRules.IsCookingIngredient(item) || ItemCategoryRules.IsHighQualityProduce(item))
                    return 1.15f;
            }
            else if (IsFestival(festivalName, "Stardew Valley Fair"))
            {
                if (ItemCategoryRules.IsArtisanGood(item))
                    return 1.15f;
            }
            else if (IsFestival(festivalName, "Spirit's Eve"))
            {
                if (ItemCategoryRules.IsPumpkin(item))
                    return 1.20f;
            }

            return 1f;
        }

        private static bool IsFestival(string actualName, string expected) =>
            actualName.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}
