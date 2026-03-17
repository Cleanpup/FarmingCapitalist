namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for crop economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableCropEconomyCategoryDefinition : ICategoryRandomizableDefinition<CropEconomicTrait>
    {
        public string Key { get; }
        public CropEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableCropEconomyCategoryDefinition(
            string key,
            CropEconomicTrait trait,
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

        public bool MatchesTraits(CropEconomicTrait traits)
        {
            return Trait != CropEconomicTrait.None && (traits & Trait) == Trait;
        }
    }
}
