namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of mineral category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class MineralEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableMineralEconomyCategoryDefinition> Definitions =
            new List<RandomizableMineralEconomyCategoryDefinition>
            {
                new(nameof(MineralEconomicTrait.Common), MineralEconomicTrait.Common),
                new(nameof(MineralEconomicTrait.Uncommon), MineralEconomicTrait.Uncommon),
                new(nameof(MineralEconomicTrait.Rare), MineralEconomicTrait.Rare),
                new(nameof(MineralEconomicTrait.Luxury), MineralEconomicTrait.Luxury)
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
