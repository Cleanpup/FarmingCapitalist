namespace FarmingCapitalist
{
    internal sealed class EquipmentMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string EquipmentItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public EquipmentEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public EquipmentMarketDefinition(
            string equipmentItemId,
            string displayName,
            int basePrice,
            EquipmentEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            EquipmentItemId = equipmentItemId;
            DisplayName = displayName;
            BasePrice = basePrice;
            Traits = traits;
            Temperament = temperament;
        }

        public bool IsAvailableInSeason(string? seasonKey)
        {
            return !string.IsNullOrWhiteSpace(seasonKey)
                && YearRoundSeasonKeys.Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        string ICategoryMarketDefinition.ItemId => EquipmentItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(EquipmentEconomicTrait trait)
        {
            return trait != EquipmentEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
