namespace FarmingCapitalist
{
    internal sealed class FishMarketDefinition : ICategoryMarketDefinition
    {
        private readonly HashSet<string> _availableSeasons;

        public string FishItemId { get; }
        public string SourceFishItemId { get; }
        public string DisplayName { get; }
        public FishEconomyClassification Classification { get; }
        public FishEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => _availableSeasons;

        public FishMarketDefinition(
            string fishItemId,
            string sourceFishItemId,
            string displayName,
            FishEconomyClassification classification,
            FishEconomicTrait traits,
            MarketTemperament temperament,
            IEnumerable<string> availableSeasons
        )
        {
            FishItemId = fishItemId;
            SourceFishItemId = sourceFishItemId;
            DisplayName = displayName;
            Classification = classification;
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

        // Maps the shared category item ID onto the existing fish item identifier.
        string ICategoryMarketDefinition.ItemId => FishItemId;

        // Maps the shared seasonal keys contract onto the existing fish season collection.
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;

        // Maps shared availability checks onto the existing fish season helper.
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(FishEconomicTrait trait)
        {
            return trait != FishEconomicTrait.None
                && (Traits & trait) == trait;
        }

        public bool IsSpringFish => HasTrait(FishEconomicTrait.Spring);
        public bool IsNightFish => HasTrait(FishEconomicTrait.Night);
        public bool IsRainFish => HasTrait(FishEconomicTrait.Rainy);
        public bool IsTrapFish => HasTrait(FishEconomicTrait.Trap);
    }
}
