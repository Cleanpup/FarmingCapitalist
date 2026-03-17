namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of crop category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class CropEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableCropEconomyCategoryDefinition> Definitions =
            new List<RandomizableCropEconomyCategoryDefinition>
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

        private static readonly Dictionary<string, RandomizableCropEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableCropEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableCropEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
