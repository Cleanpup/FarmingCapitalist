namespace FarmingCapitalist
{
    /// <summary>
    /// Central source of monster-loot category keys eligible for randomized save-profile modifiers.
    /// </summary>
    internal static class MonsterLootEconomyCategoryRegistry
    {
        private static readonly IReadOnlyList<RandomizableMonsterLootEconomyCategoryDefinition> Definitions =
            new List<RandomizableMonsterLootEconomyCategoryDefinition>
            {
                new(nameof(MonsterLootEconomicTrait.BasicMonsterDrop), MonsterLootEconomicTrait.BasicMonsterDrop),
                new(nameof(MonsterLootEconomicTrait.SlimeRelatedItem), MonsterLootEconomicTrait.SlimeRelatedItem),
                new(nameof(MonsterLootEconomicTrait.EssenceMagicalDrop), MonsterLootEconomicTrait.EssenceMagicalDrop)
            };

        private static readonly Dictionary<string, RandomizableMonsterLootEconomyCategoryDefinition> DefinitionsByKey =
            Definitions.ToDictionary(definition => definition.Key, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<RandomizableMonsterLootEconomyCategoryDefinition> GetRandomizableCategories() => Definitions;

        public static bool TryGetCategory(string? key, out RandomizableMonsterLootEconomyCategoryDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(key) && DefinitionsByKey.TryGetValue(key, out definition!))
                return true;

            definition = null!;
            return false;
        }
    }
}
