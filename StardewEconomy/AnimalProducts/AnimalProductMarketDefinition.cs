namespace FarmingCapitalist
{
    internal sealed class AnimalProductMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string AnimalProductItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public AnimalProductEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public AnimalProductMarketDefinition(
            string animalProductItemId,
            string displayName,
            int basePrice,
            AnimalProductEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            AnimalProductItemId = animalProductItemId;
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

        string ICategoryMarketDefinition.ItemId => AnimalProductItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(AnimalProductEconomicTrait trait)
        {
            return trait != AnimalProductEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
