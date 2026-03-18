namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of mining category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class MineralEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableMineralEconomyCategoryDefinition> Definitions =
            new List<RandomizableMineralEconomyCategoryDefinition>
            {
                new(nameof(MineralEconomicTrait.Stone), MineralEconomicTrait.Stone),
                new(nameof(MineralEconomicTrait.Coal), MineralEconomicTrait.Coal),
                new(nameof(MineralEconomicTrait.Ore), MineralEconomicTrait.Ore),
                new(nameof(MineralEconomicTrait.Bar), MineralEconomicTrait.Bar),
                new(nameof(MineralEconomicTrait.Mineral), MineralEconomicTrait.Mineral),
                new(nameof(MineralEconomicTrait.Gem), MineralEconomicTrait.Gem),
                new(nameof(MineralEconomicTrait.Geode), MineralEconomicTrait.Geode)
            };

        private static readonly Dictionary<string, RandomizableMineralEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableMineralEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableMineralEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
