namespace FarmingCapitalist
{
    internal sealed class MarketCropDefinition
    {
        private readonly HashSet<string> _growingSeasons;

        public string ProduceItemId { get; }
        public string DisplayName { get; }
        public string SeedItemId { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> GrowingSeasons => _growingSeasons;

        public MarketCropDefinition(
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
    }
}
