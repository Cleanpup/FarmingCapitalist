namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of fish category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class FishEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableFishEconomyCategoryDefinition> Definitions =
            new List<RandomizableFishEconomyCategoryDefinition>
            {
                new(nameof(FishEconomicTrait.Spring), FishEconomicTrait.Spring),
                new(nameof(FishEconomicTrait.Summer), FishEconomicTrait.Summer),
                new(nameof(FishEconomicTrait.Fall), FishEconomicTrait.Fall),
                new(nameof(FishEconomicTrait.Winter), FishEconomicTrait.Winter),
                new(nameof(FishEconomicTrait.Morning), FishEconomicTrait.Morning),
                new(nameof(FishEconomicTrait.Day), FishEconomicTrait.Day),
                new(nameof(FishEconomicTrait.Evening), FishEconomicTrait.Evening),
                new(nameof(FishEconomicTrait.Night), FishEconomicTrait.Night),
                new(nameof(FishEconomicTrait.Sunny), FishEconomicTrait.Sunny),
                new(nameof(FishEconomicTrait.Rainy), FishEconomicTrait.Rainy),
                new(nameof(FishEconomicTrait.Trap), FishEconomicTrait.Trap),
                new(nameof(FishEconomicTrait.LineCaught), FishEconomicTrait.LineCaught)
            };

        private static readonly Dictionary<string, RandomizableFishEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableFishEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableFishEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
