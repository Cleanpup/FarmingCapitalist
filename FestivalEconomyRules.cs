using System;
using StardewValley;
using SObject = StardewValley.Object;

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
                if (asItem is SObject obj && IsEgg(obj))
                    return 1.15f;
            }

            return 1f;
        }

        public static float GetFestivalSellModifier(Item item, EconomyContext context)
        {
            if (!context.FestivalTomorrow || string.IsNullOrWhiteSpace(context.FestivalTomorrowName))
                return 1f;

            if (item is not SObject obj)
                return 1f;

            string festivalName = context.FestivalTomorrowName!;
            if (IsFestival(festivalName, "Egg Festival"))
            {
                if (IsEgg(obj))
                    return 1.15f;
            }

            if (IsFestival(festivalName, "Flower Dance"))
            {
                if (IsFlower(obj))
                    return 1.20f;
            }
            else if (IsFestival(festivalName, "Luau"))
            {
                if (IsCookingIngredient(obj) || IsHighQualityProduce(obj))
                    return 1.15f;
            }
            else if (IsFestival(festivalName, "Stardew Valley Fair"))
            {
                if (IsArtisanGood(obj))
                    return 1.15f;
            }
            else if (IsFestival(festivalName, "Spirit's Eve"))
            {
                if (IsPumpkin(obj))
                    return 1.20f;
            }

            return 1f;
        }

        private static bool IsFestival(string actualName, string expected) =>
            actualName.Contains(expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsSeed(SObject obj) => obj.Category == SObject.SeedsCategory;

        private static bool IsEgg(SObject obj) => obj.Category == SObject.EggCategory;

        private static bool IsFlower(SObject obj) => obj.Category == SObject.flowersCategory;

        private static bool IsArtisanGood(SObject obj) => obj.Category == SObject.artisanGoodsCategory;

        private static bool IsCookingIngredient(SObject obj)
        {
            int category = obj.Category;
            return category == SObject.CookingCategory
                || category == SObject.VegetableCategory
                || category == SObject.FruitsCategory
                || category == SObject.flowersCategory
                || category == SObject.GreensCategory
                || category == SObject.EggCategory
                || category == SObject.MilkCategory
                || category == SObject.meatCategory
                || category == SObject.FishCategory;
        }

        private static bool IsHighQualityProduce(SObject obj) =>
            IsProduce(obj) && obj.Quality >= SObject.highQuality;

        private static bool IsProduce(SObject obj)
        {
            int category = obj.Category;
            return category == SObject.VegetableCategory
                || category == SObject.FruitsCategory
                || category == SObject.flowersCategory
                || category == SObject.GreensCategory;
        }

        private static bool IsPumpkin(SObject obj)
        {
            if (obj.ParentSheetIndex == 276 || string.Equals(obj.ItemId, "276", StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(obj.ItemId, "Pumpkin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
