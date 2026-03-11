namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of category keys eligible for randomized save-profile modifiers.
    /// Add new categories here to include them in generation/application.
    /// </summary>
    internal static class EconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableEconomyCategoryDefinition> Definitions =
            new List<RandomizableEconomyCategoryDefinition>
            {
                new(nameof(CropEconomicTrait.SingleHarvest), CropEconomicTrait.SingleHarvest),
                new(nameof(CropEconomicTrait.Regrowth), CropEconomicTrait.Regrowth),
                new(nameof(CropEconomicTrait.SingleYield), CropEconomicTrait.SingleYield),
                new(nameof(CropEconomicTrait.MultiYield), CropEconomicTrait.MultiYield),
                new(nameof(CropEconomicTrait.FastCrop), CropEconomicTrait.FastCrop),
                new(nameof(CropEconomicTrait.MediumCrop), CropEconomicTrait.MediumCrop),
                new(nameof(CropEconomicTrait.SlowCrop), CropEconomicTrait.SlowCrop),
                new(nameof(CropEconomicTrait.CheapSeed), CropEconomicTrait.CheapSeed),
                new(nameof(CropEconomicTrait.MidSeed), CropEconomicTrait.MidSeed),
                new(nameof(CropEconomicTrait.ExpensiveSeed), CropEconomicTrait.ExpensiveSeed),
                new(nameof(CropEconomicTrait.LowHarvestFrequency), CropEconomicTrait.LowHarvestFrequency),
                new(nameof(CropEconomicTrait.MediumHarvestFrequency), CropEconomicTrait.MediumHarvestFrequency),
                new(nameof(CropEconomicTrait.HighHarvestFrequency), CropEconomicTrait.HighHarvestFrequency)
            };

        private static readonly Dictionary<string, RandomizableEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
