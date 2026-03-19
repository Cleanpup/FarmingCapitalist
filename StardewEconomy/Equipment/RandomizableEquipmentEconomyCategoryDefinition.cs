namespace FarmingCapitalist
{
    /// <summary>
    /// Registry metadata for equipment economy categories that can be randomized by a save profile.
    /// </summary>
    internal sealed class RandomizableEquipmentEconomyCategoryDefinition : ICategoryRandomizableDefinition<EquipmentEconomicTrait>
    {
        public string Key { get; }
        public EquipmentEconomicTrait Trait { get; }
        public bool SupportsBuy { get; }
        public bool SupportsSell { get; }

        public RandomizableEquipmentEconomyCategoryDefinition(
            string key,
            EquipmentEconomicTrait trait,
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

        public bool MatchesTraits(EquipmentEconomicTrait traits)
        {
            return Trait != EquipmentEconomicTrait.None
                && (traits & Trait) == Trait;
        }
    }
}
