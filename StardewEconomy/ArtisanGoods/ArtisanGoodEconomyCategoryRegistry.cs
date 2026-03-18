namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of artisan-good category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class ArtisanGoodEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableArtisanGoodEconomyCategoryDefinition> Definitions =
            new List<RandomizableArtisanGoodEconomyCategoryDefinition>
            {
                new(nameof(ArtisanGoodEconomicTrait.AlcoholBeverage), ArtisanGoodEconomicTrait.AlcoholBeverage),
                new(nameof(ArtisanGoodEconomicTrait.Preserve), ArtisanGoodEconomicTrait.Preserve),
                new(nameof(ArtisanGoodEconomicTrait.DairyArtisanGood), ArtisanGoodEconomicTrait.DairyArtisanGood),
                new(nameof(ArtisanGoodEconomicTrait.ClothLoomProduct), ArtisanGoodEconomicTrait.ClothLoomProduct),
                new(nameof(ArtisanGoodEconomicTrait.OilProduct), ArtisanGoodEconomicTrait.OilProduct),
                new(nameof(ArtisanGoodEconomicTrait.SpecialtyProcessedGood), ArtisanGoodEconomicTrait.SpecialtyProcessedGood)
            };

        private static readonly Dictionary<string, RandomizableArtisanGoodEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableArtisanGoodEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableArtisanGoodEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
