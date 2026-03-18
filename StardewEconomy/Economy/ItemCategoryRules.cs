using System;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared item/category checks used by multiple economy rule layers.
    /// Keep this file focused on base vanilla category predicates.
    /// </summary>
    internal static class ItemCategoryRules
    {
        public static int GetCategory(Item item)
        {
            return item?.Category ?? int.MinValue;
        }

        public static bool HasCategory(Item item, int category)
        {
            return GetCategory(item) == category;
        }

        public static bool MatchesAnyCategory(Item item, params int[] categories)
        {
            if (item == null || categories == null || categories.Length == 0)
                return false;

            int category = GetCategory(item);
            for (int i = 0; i < categories.Length; i++)
            {
                if (category == categories[i])
                    return true;
            }

            return false;
        }

        public static bool IsSeed(Item item) => HasCategory(item, SObject.SeedsCategory);

        public static bool IsEgg(Item item) => HasCategory(item, SObject.EggCategory);

        public static bool IsMilk(Item item) => HasCategory(item, SObject.MilkCategory);

        public static bool IsMeat(Item item) => HasCategory(item, SObject.meatCategory);

        public static bool IsAnimalGood(Item item) =>
            MatchesAnyCategory(item, SObject.EggCategory, SObject.MilkCategory, SObject.meatCategory);

        public static bool IsVegetable(Item item) => HasCategory(item, SObject.VegetableCategory);

        public static bool IsFruit(Item item) => HasCategory(item, SObject.FruitsCategory);

        public static bool IsFlower(Item item) => HasCategory(item, SObject.flowersCategory);

        public static bool IsFish(Item item) => HasCategory(item, SObject.FishCategory);

        public static bool IsArtisanGood(Item item) => HasCategory(item, SObject.artisanGoodsCategory);

        public static bool IsEquipment(Item item) => HasCategory(item, SObject.equipmentCategory);

        public static bool IsMonsterLoot(Item item) => HasCategory(item, SObject.monsterLootCategory);

        public static bool IsBait(Item item) => HasCategory(item, SObject.baitCategory);

        public static bool IsMetalResource(Item item) => HasCategory(item, SObject.metalResources);

        public static bool IsTackle(Item item) => HasCategory(item, SObject.tackleCategory);

        public static bool IsFertilizer(Item item) => HasCategory(item, SObject.fertilizerCategory);

        public static bool IsFishingGear(Item item) =>
            MatchesAnyCategory(item, SObject.baitCategory, SObject.tackleCategory);

        public static bool IsResource(Item item) =>
            MatchesAnyCategory(item, SObject.metalResources, SObject.monsterLootCategory);

        public static bool IsSyrup(Item item) => HasCategory(item, SObject.syrupCategory);

        public static bool IsGem(Item item) => HasCategory(item, SObject.GemCategory);

        public static bool IsMineral(Item item) => HasCategory(item, SObject.mineralsCategory);

        public static bool IsStone(Item item) => HasItemId(item, "32");

        public static bool IsCoal(Item item) => HasItemId(item, "382");

        public static bool IsOre(Item item) => item is not null && item.HasContextTag("ore_item");

        public static bool IsBar(Item item) => item is not null && item.HasContextTag("furnace_item");

        public static bool IsGeode(Item item) =>
            item is not null
            && item.HasContextTag("geode")
            && !item.HasContextTag("geode_crusher_ignored")
            && !item.QualifiedItemId.Contains("MysteryBox", StringComparison.OrdinalIgnoreCase);

        public static bool IsMiningMaterial(Item item) =>
            IsStone(item)
            || IsCoal(item)
            || IsOre(item)
            || IsBar(item)
            || IsGem(item)
            || IsMineral(item)
            || IsGeode(item);

        public static bool IsProduce(Item item) =>
            MatchesAnyCategory(item, SObject.VegetableCategory, SObject.FruitsCategory, SObject.flowersCategory, SObject.GreensCategory);

        public static bool IsCookingIngredient(Item item)
        {
            return MatchesAnyCategory(
                item,
                SObject.CookingCategory,
                SObject.VegetableCategory,
                SObject.FruitsCategory,
                SObject.flowersCategory,
                SObject.GreensCategory,
                SObject.EggCategory,
                SObject.MilkCategory,
                SObject.meatCategory,
                SObject.FishCategory
            );
        }

        public static bool IsHighQualityProduce(Item item)
        {
            if (item is not SObject obj)
                return false;

            return IsProduce(item) && obj.Quality >= SObject.highQuality;
        }

        public static bool IsPumpkin(Item item)
        {
            if (item is not SObject obj)
                return false;

            if (obj.ParentSheetIndex == 276 || string.Equals(obj.ItemId, "276", StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(obj.ItemId, "Pumpkin", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasItemId(Item item, string itemId)
        {
            return item is SObject obj
                && !string.IsNullOrWhiteSpace(obj.ItemId)
                && string.Equals(obj.ItemId, itemId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
