using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared category-owned contract for sale tracking and item identity resolution.
    /// </summary>
    internal interface ICategorySupplyTracker
    {
        /// <summary>Track a sale for a concrete item instance.</summary>
        void TrackSale(Item? item, int quantity, string source);

        /// <summary>Track a sale for a normalized category item ID and display name.</summary>
        void TrackCategorySale(string itemId, string displayName, int quantity, string source);

        /// <summary>Track a batch of items under a single source label.</summary>
        void TrackItems(IEnumerable<Item> items, string source);

        /// <summary>Try resolve a concrete item instance into a normalized item ID and display name.</summary>
        bool TryGetItemInfo(Item? item, out string itemId, out string displayName);

        /// <summary>Try resolve raw player input into a normalized item ID and display name.</summary>
        bool TryResolveItemId(string? rawInput, out string itemId, out string displayName);

        /// <summary>Try normalize a raw category item ID.</summary>
        bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId);

        /// <summary>Get a display name for a normalized category item ID.</summary>
        string GetDisplayName(string? itemId);
    }
}
