namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of cooking-food category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class CookingFoodEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableCookingFoodEconomyCategoryDefinition> Definitions =
            new List<RandomizableCookingFoodEconomyCategoryDefinition>
            {
                new(nameof(CookingFoodEconomicTrait.Meal), CookingFoodEconomicTrait.Meal),
                new(nameof(CookingFoodEconomicTrait.Dessert), CookingFoodEconomicTrait.Dessert),
                new(nameof(CookingFoodEconomicTrait.Drink), CookingFoodEconomicTrait.Drink),
                new(nameof(CookingFoodEconomicTrait.BuffFood), CookingFoodEconomicTrait.BuffFood),
                new(nameof(CookingFoodEconomicTrait.RecipeOutput), CookingFoodEconomicTrait.RecipeOutput)
            };

        private static readonly Dictionary<string, RandomizableCookingFoodEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableCookingFoodEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableCookingFoodEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
