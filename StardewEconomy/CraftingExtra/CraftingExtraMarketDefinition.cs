namespace FarmingCapitalist
{
    internal sealed class CraftingExtraMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string CraftingExtraItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public CraftingExtraEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public CraftingExtraMarketDefinition(
            string craftingExtraItemId,
            string displayName,
            int basePrice,
            CraftingExtraEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            CraftingExtraItemId = craftingExtraItemId;
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

        string ICategoryMarketDefinition.ItemId => CraftingExtraItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(CraftingExtraEconomicTrait trait)
        {
            return trait != CraftingExtraEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
