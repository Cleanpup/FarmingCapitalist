namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared read-only contract for category market definitions.
    /// </summary>
    internal interface ICategoryMarketDefinition
    {
        /// <summary>Get the normalized category item ID.</summary>
        string ItemId { get; }

        /// <summary>Get the display name used for logs and debug output.</summary>
        string DisplayName { get; }

        /// <summary>Get the temperament used by market simulation tuning.</summary>
        MarketTemperament Temperament { get; }

        /// <summary>Get the normalized season keys relevant to this definition.</summary>
        IReadOnlyCollection<string> SeasonalKeys { get; }

        /// <summary>Get whether the definition is available in the supplied season.</summary>
        bool IsAvailableInSeason(string? seasonKey);
    }
}
