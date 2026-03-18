namespace FarmingCapitalist
{
    internal sealed class ForageableMarketDefinition : ICategoryMarketDefinition
    {
        private readonly HashSet<string> _availableSeasons;

        public string ForageableItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public ForageableEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => _availableSeasons;

        public ForageableMarketDefinition(
            string forageableItemId,
            string displayName,
            int basePrice,
            ForageableEconomicTrait traits,
            MarketTemperament temperament,
            IEnumerable<string> availableSeasons
        )
        {
            ForageableItemId = forageableItemId;
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

        string ICategoryMarketDefinition.ItemId => ForageableItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(ForageableEconomicTrait trait)
        {
            return trait != ForageableEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
