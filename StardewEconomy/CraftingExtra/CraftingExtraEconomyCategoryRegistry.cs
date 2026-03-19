namespace FarmingCapitalist
{
    /// <summary>
    /// Registry entry for the neutral crafting-extra profile layer.
    /// The save-profile generator intentionally leaves this category unrandomized per the requested policy.
    /// </summary>
    internal static class CraftingExtraEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableCraftingExtraEconomyCategoryDefinition> Definitions =
            new List<RandomizableCraftingExtraEconomyCategoryDefinition>
            {
                new(nameof(CraftingExtraEconomicTrait.Material), CraftingExtraEconomicTrait.Material)
            };

        private static readonly Dictionary<string, RandomizableCraftingExtraEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableCraftingExtraEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableCraftingExtraEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
