namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for monster-loot economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableMonsterLootEconomyCategoryDefinition : ICategoryRandomizableDefinition<MonsterLootEconomicTrait>
    {
        public string Key { get; }
        public MonsterLootEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableMonsterLootEconomyCategoryDefinition(
            string key,
            MonsterLootEconomicTrait trait,
            bool supportsBuy = true,
            bool supportsSell = true
        )
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Category key cannot be null or whitespace.", nameof(key));

            Key = key;
            Trait = trait;
            SupportsBuy = supportsBuy;
            SupportsSell = supportsSell;
        }

        public bool MatchesTraits(MonsterLootEconomicTrait traits)
        {
            return Trait != MonsterLootEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
