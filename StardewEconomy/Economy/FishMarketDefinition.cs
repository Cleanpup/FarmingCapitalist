namespace FarmingCapitalist
{
    internal sealed class FishMarketDefinition
    {
        private readonly HashSet<string> _availableSeasons;

        public string FishItemId { get; }
        public string SourceFishItemId { get; }
        public string DisplayName { get; }
        public FishEconomyClassification Classification { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => _availableSeasons;

        public FishMarketDefinition(
            string fishItemId,
            string sourceFishItemId,
            string displayName,
            FishEconomyClassification classification,
            MarketTemperament temperament,
            IEnumerable<string> availableSeasons
        )
        {
            FishItemId = fishItemId;
            SourceFishItemId = sourceFishItemId;
            DisplayName = displayName;
            Classification = classification;
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
    }
}
