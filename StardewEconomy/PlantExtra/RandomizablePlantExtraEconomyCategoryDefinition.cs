namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for plantExtra economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizablePlantExtraEconomyCategoryDefinition : ICategoryRandomizableDefinition<PlantExtraEconomicTrait>
    {
        public string Key { get; }
        public PlantExtraEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizablePlantExtraEconomyCategoryDefinition(
            string key,
            PlantExtraEconomicTrait trait,
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

        public bool MatchesTraits(PlantExtraEconomicTrait traits)
        {
            return Trait != PlantExtraEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
