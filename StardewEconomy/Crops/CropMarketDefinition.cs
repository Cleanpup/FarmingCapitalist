namespace FarmingCapitalist
{
    internal sealed class CropMarketDefinition : ICategoryMarketDefinition
    {
        private readonly HashSet<string> _growingSeasons;

        public string ProduceItemId { get; }
        public string DisplayName { get; }
        public string SeedItemId { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> GrowingSeasons => _growingSeasons;

        public CropMarketDefinition(
            string produceItemId,
            string displayName,
            string seedItemId,
            MarketTemperament temperament,
            IEnumerable<string> growingSeasons
        )
        {
            ProduceItemId = produceItemId;
            DisplayName = displayName;
            SeedItemId = seedItemId;
            Temperament = temperament;
            _growingSeasons = new HashSet<string>(
                growingSeasons.Where(season => !string.IsNullOrWhiteSpace(season)).Select(season => season.Trim()),
                StringComparer.OrdinalIgnoreCase
            );
        }

        public bool GrowsInSeason(string? currentSeason)
        {
            return !string.IsNullOrWhiteSpace(currentSeason)
                && _growingSeasons.Contains(currentSeason.Trim());
        }

        // Maps the shared category item ID onto the existing produce item identifier.
        string ICategoryMarketDefinition.ItemId => ProduceItemId;

        // Maps the shared seasonal keys contract onto the existing crop season collection.
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => GrowingSeasons;

        // Maps shared availability checks onto the existing crop season helper.
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => GrowsInSeason(seasonKey);
    }
}
