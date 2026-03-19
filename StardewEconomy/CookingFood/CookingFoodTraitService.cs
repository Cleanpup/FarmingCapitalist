using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping cooking-food traits.
    /// </summary>
    internal static class CookingFoodTraitService
    {
        internal static IMonitor? Monitor;

        public static CookingFoodEconomicTrait GetTraits(Item? item)
        {
            if (!CookingFoodEconomyItemRules.TryGetCookingFoodObject(item, out SObject cookingFoodObject))
                return CookingFoodEconomicTrait.None;

            return GetTraitsForItem(cookingFoodObject);
        }

        public static CookingFoodEconomicTrait GetTraits(string? cookingFoodItemId)
        {
            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return CookingFoodEconomicTrait.None;

            if (!CookingFoodEconomyItemRules.TryCreateCookingFoodObject(normalizedCookingFoodItemId, out SObject? cookingFoodObject)
                || cookingFoodObject is null)
            {
                return CookingFoodEconomicTrait.None;
            }

            return GetTraitsForItem(cookingFoodObject);
        }

        public static bool HasTrait(Item? item, CookingFoodEconomicTrait trait)
        {
            if (trait == CookingFoodEconomicTrait.None)
                return false;

            CookingFoodEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? cookingFoodItemId, CookingFoodEconomicTrait trait)
        {
            if (trait == CookingFoodEconomicTrait.None)
                return false;

            CookingFoodEconomicTrait traits = GetTraits(cookingFoodItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(CookingFoodEconomicTrait traits)
        {
            if (traits == CookingFoodEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (CookingFoodEconomicTrait trait in Enum.GetValues<CookingFoodEconomicTrait>())
            {
                if (trait == CookingFoodEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Cooking-food traits: <null item> -> None";

            CookingFoodEconomicTrait traits = GetTraits(item);
            return $"Cooking-food traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static CookingFoodEconomicTrait GetTraitsForItem(SObject cookingFoodObject)
        {
            CookingFoodEconomicTrait traits = CookingFoodEconomicTrait.None;

            if (CookingFoodEconomyItemRules.IsMeal(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.Meal;

            if (CookingFoodEconomyItemRules.IsDessert(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.Dessert;

            if (CookingFoodEconomyItemRules.IsDrink(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.Drink;

            if (CookingFoodEconomyItemRules.IsBuffFood(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.BuffFood;

            if (CookingFoodEconomyItemRules.IsRecipeOutput(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.RecipeOutput;

            if (CookingFoodEconomyItemRules.IsCookingIngredient(cookingFoodObject))
                traits |= CookingFoodEconomicTrait.CookingIngredient;

            return traits;
        }
    }
}
