namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for animal product economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableAnimalProductEconomyCategoryDefinition : ICategoryRandomizableDefinition<AnimalProductEconomicTrait>
    {
        public string Key { get; }
        public AnimalProductEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableAnimalProductEconomyCategoryDefinition(
            string key,
            AnimalProductEconomicTrait trait,
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

        public bool MatchesTraits(AnimalProductEconomicTrait traits)
        {
            return Trait != AnimalProductEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
