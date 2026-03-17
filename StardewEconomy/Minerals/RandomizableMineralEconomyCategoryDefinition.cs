namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for mineral economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableMineralEconomyCategoryDefinition : ICategoryRandomizableDefinition<MineralEconomicTrait>
    {
        public string Key { get; }
        public MineralEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableMineralEconomyCategoryDefinition(
            string key,
            MineralEconomicTrait trait,
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

        public bool MatchesTraits(MineralEconomicTrait traits)
        {
            return Trait != MineralEconomicTrait.None && (traits & Trait) == Trait;
        }
    }
}
