namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for cooking-food economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableCookingFoodEconomyCategoryDefinition : ICategoryRandomizableDefinition<CookingFoodEconomicTrait>
    {
        public string Key { get; }
        public CookingFoodEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableCookingFoodEconomyCategoryDefinition(
            string key,
            CookingFoodEconomicTrait trait,
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

        public bool MatchesTraits(CookingFoodEconomicTrait traits)
        {
            return Trait != CookingFoodEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
