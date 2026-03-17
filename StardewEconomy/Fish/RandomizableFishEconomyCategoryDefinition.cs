namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for fish economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableFishEconomyCategoryDefinition : ICategoryRandomizableDefinition<FishEconomicTrait>
    {
        public string Key { get; }
        public FishEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableFishEconomyCategoryDefinition(
            string key,
            FishEconomicTrait trait,
            bool supportsBuy = false,
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

        public bool MatchesTraits(FishEconomicTrait traits)
        {
            return Trait != FishEconomicTrait.None && (traits & Trait) == Trait;
        }
    }
}
