namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of animal product category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class AnimalProductEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableAnimalProductEconomyCategoryDefinition> Definitions =
            new List<RandomizableAnimalProductEconomyCategoryDefinition>
            {
                new(nameof(AnimalProductEconomicTrait.Egg), AnimalProductEconomicTrait.Egg),
                new(nameof(AnimalProductEconomicTrait.Milk), AnimalProductEconomicTrait.Milk),
                new(nameof(AnimalProductEconomicTrait.FiberAnimalProduct), AnimalProductEconomicTrait.FiberAnimalProduct),
                new(nameof(AnimalProductEconomicTrait.CoopProduct), AnimalProductEconomicTrait.CoopProduct),
                new(nameof(AnimalProductEconomicTrait.BarnProduct), AnimalProductEconomicTrait.BarnProduct),
                new(nameof(AnimalProductEconomicTrait.SpecialtyAnimalGood), AnimalProductEconomicTrait.SpecialtyAnimalGood)
            };

        private static readonly Dictionary<string, RandomizableAnimalProductEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableAnimalProductEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableAnimalProductEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
