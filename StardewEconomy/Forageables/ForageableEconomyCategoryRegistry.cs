namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of forageable category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class ForageableEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableForageableEconomyCategoryDefinition> Definitions =
            new List<RandomizableForageableEconomyCategoryDefinition>
            {
                new(nameof(ForageableEconomicTrait.SeasonalForage), ForageableEconomicTrait.SeasonalForage),
                new(nameof(ForageableEconomicTrait.BeachForage), ForageableEconomicTrait.BeachForage),
                new(nameof(ForageableEconomicTrait.ForestForage), ForageableEconomicTrait.ForestForage),
                new(nameof(ForageableEconomicTrait.DesertForage), ForageableEconomicTrait.DesertForage),
                new(nameof(ForageableEconomicTrait.GingerIslandForage), ForageableEconomicTrait.GingerIslandForage),
                new(nameof(ForageableEconomicTrait.GatheredFlowersWildEdibles), ForageableEconomicTrait.GatheredFlowersWildEdibles)
            };

        private static readonly Dictionary<string, RandomizableForageableEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableForageableEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableForageableEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
