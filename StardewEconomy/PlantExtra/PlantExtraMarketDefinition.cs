namespace FarmingCapitalist
{
    internal sealed class PlantExtraMarketDefinition : ICategoryMarketDefinition
    {
        private readonly HashSet<string> _availableSeasons;

        public string PlantExtraItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public PlantExtraEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => _availableSeasons;

        public PlantExtraMarketDefinition(
            string plantExtraItemId,
            string displayName,
            int basePrice,
            PlantExtraEconomicTrait traits,
            MarketTemperament temperament,
            IEnumerable<string> availableSeasons
        )
        {
            PlantExtraItemId = plantExtraItemId;
            DisplayName = displayName;
            BasePrice = basePrice;
            Traits = traits;
            Temperament = temperament;
            _availableSeasons = new HashSet<string>(
                availableSeasons
                    .Where(season => !string.IsNullOrWhiteSpace(season))
                    .Select(season => season.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase
            );
        }

        public bool IsAvailableInSeason(string? seasonKey)
        {
            return !string.IsNullOrWhiteSpace(seasonKey)
                && _availableSeasons.Contains(seasonKey.Trim());
        }

        string ICategoryMarketDefinition.ItemId => PlantExtraItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(PlantExtraEconomicTrait trait)
        {
            return trait != PlantExtraEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
