namespace FarmingCapitalist
{
    internal sealed class MonsterLootMarketDefinition : ICategoryMarketDefinition
    {
        private static readonly IReadOnlyCollection<string> YearRoundSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        public string MonsterLootItemId { get; }
        public string DisplayName { get; }
        public int BasePrice { get; }
        public MonsterLootEconomicTrait Traits { get; }
        public MarketTemperament Temperament { get; }
        public IReadOnlyCollection<string> AvailableSeasons => YearRoundSeasonKeys;

        public MonsterLootMarketDefinition(
            string monsterLootItemId,
            string displayName,
            int basePrice,
            MonsterLootEconomicTrait traits,
            MarketTemperament temperament
        )
        {
            MonsterLootItemId = monsterLootItemId;
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

        string ICategoryMarketDefinition.ItemId => MonsterLootItemId;
        IReadOnlyCollection<string> ICategoryMarketDefinition.SeasonalKeys => AvailableSeasons;
        bool ICategoryMarketDefinition.IsAvailableInSeason(string? seasonKey) => IsAvailableInSeason(seasonKey);

        public bool HasTrait(MonsterLootEconomicTrait trait)
        {
            return trait != MonsterLootEconomicTrait.None
                && (Traits & trait) == trait;
        }
    }
}
