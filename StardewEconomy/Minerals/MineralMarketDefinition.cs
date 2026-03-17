namespace FarmingCapitalist
{
    internal sealed class MineralMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string MineralItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public MineralEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public MineralMarketDefinition(
            string mineralItemId,
            string displayName,
            int basePrice,
            MineralEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            MineralItemId = mineralItemId;
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

        // Maps the shared category item ID onto the existing mineral item identifier.
        string ICategoryMarketDefinition.ItemId => MineralItemId;

        // Maps the shared seasonal keys contract onto the existing year-round mineral season collection.
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;

        // Maps shared availability checks onto the existing mineral season helper.
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);
    }
}
