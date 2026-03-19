namespace FarmingCapitalist
{
    internal sealed class CookingFoodMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string CookingFoodItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public CookingFoodEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public CookingFoodMarketDefinition(
            string cookingFoodItemId,
            string displayName,
            int basePrice,
            CookingFoodEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            CookingFoodItemId = cookingFoodItemId;
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

        string ICategoryMarketDefinition.ItemId => CookingFoodItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(CookingFoodEconomicTrait trait)
        {
            return trait != CookingFoodEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
