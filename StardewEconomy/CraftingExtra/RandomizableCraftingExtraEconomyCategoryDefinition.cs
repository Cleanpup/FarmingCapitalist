namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for crafting-extra profile categories.
    /// The current generation policy keeps these neutral, but the registry shape stays compatible with the existing profile architecture.
    /// </summary>
    internal sealed class RandomizableCraftingExtraEconomyCategoryDefinition : ICategoryRandomizableDefinition<CraftingExtraEconomicTrait>
    {
        public string Key { get; }
        public CraftingExtraEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableCraftingExtraEconomyCategoryDefinition(
            string key,
            CraftingExtraEconomicTrait trait,
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

        public bool MatchesTraits(CraftingExtraEconomicTrait traits)
        {
            return Trait != CraftingExtraEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
