namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for artisan-good economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableArtisanGoodEconomyCategoryDefinition : ICategoryRandomizableDefinition<ArtisanGoodEconomicTrait>
    {
        public string Key { get; }
        public ArtisanGoodEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableArtisanGoodEconomyCategoryDefinition(
            string key,
            ArtisanGoodEconomicTrait trait,
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

        public bool MatchesTraits(ArtisanGoodEconomicTrait traits)
        {
            return Trait != ArtisanGoodEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
