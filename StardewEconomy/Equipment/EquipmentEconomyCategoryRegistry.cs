namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of equipment category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class EquipmentEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableEquipmentEconomyCategoryDefinition> Definitions =
            new List<RandomizableEquipmentEconomyCategoryDefinition>
            {
                new(nameof(EquipmentEconomicTrait.Weapon), EquipmentEconomicTrait.Weapon),
                new(nameof(EquipmentEconomicTrait.Ring), EquipmentEconomicTrait.Ring),
                new(nameof(EquipmentEconomicTrait.Boots), EquipmentEconomicTrait.Boots),
                new(nameof(EquipmentEconomicTrait.WearableEquipment), EquipmentEconomicTrait.WearableEquipment)
            };

        private static readonly Dictionary<string, RandomizableEquipmentEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableEquipmentEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableEquipmentEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
