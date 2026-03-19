namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of plant-extra category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class PlantExtraEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizablePlantExtraEconomyCategoryDefinition> Definitions =
            new List<RandomizablePlantExtraEconomyCategoryDefinition>
            {
                new(nameof(PlantExtraEconomicTrait.TreeFruit), PlantExtraEconomicTrait.TreeFruit),
                new(nameof(PlantExtraEconomicTrait.TreeSapling), PlantExtraEconomicTrait.TreeSapling),
                new(nameof(PlantExtraEconomicTrait.Flower), PlantExtraEconomicTrait.Flower),
                new(nameof(PlantExtraEconomicTrait.FlowerSeedSpecialSeed), PlantExtraEconomicTrait.FlowerSeedSpecialSeed),
                new(nameof(PlantExtraEconomicTrait.Mushroom), PlantExtraEconomicTrait.Mushroom),
                new(nameof(PlantExtraEconomicTrait.TappedProduct), PlantExtraEconomicTrait.TappedProduct),
                new(nameof(PlantExtraEconomicTrait.Fertilizer), PlantExtraEconomicTrait.Fertilizer)
            };

        private static readonly Dictionary<string, RandomizablePlantExtraEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizablePlantExtraEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizablePlantExtraEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
