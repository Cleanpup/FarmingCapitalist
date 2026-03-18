namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for forageable economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableForageableEconomyCategoryDefinition : ICategoryRandomizableDefinition<ForageableEconomicTrait>
    {
        public string Key { get; }
        public ForageableEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableForageableEconomyCategoryDefinition(
            string key,
            ForageableEconomicTrait trait,
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

        public bool MatchesTraits(ForageableEconomicTrait traits)
        {
            return Trait != ForageableEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
