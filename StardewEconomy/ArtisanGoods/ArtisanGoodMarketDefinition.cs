namespace FarmingCapitalist
{
    internal sealed class ArtisanGoodMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string ArtisanGoodItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public ArtisanGoodEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public ArtisanGoodMarketDefinition(
            string artisanGoodItemId,
            string displayName,
            int basePrice,
            ArtisanGoodEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            ArtisanGoodItemId = artisanGoodItemId;
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

        string ICategoryMarketDefinition.ItemId => ArtisanGoodItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(ArtisanGoodEconomicTrait trait)
        {
            return trait != ArtisanGoodEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
