using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared artisan-good tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class ArtisanGoodSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetArtisanGoodInfo(item, out string artisanGoodItemId, out string displayName))
                return;

            TrackArtisanGoodSale(artisanGoodItemId, displayName, quantity, source);
        }

        public static void TrackArtisanGoodSale(string artisanGoodItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return;

            if (!ArtisanGoodEconomyItemRules.IsArtisanGoodItemId(normalizedArtisanGoodItemId))
                return;

            ArtisanGoodSupplyDataService.AddSupply(normalizedArtisanGoodItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByArtisanGood = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetArtisanGoodInfo(item, out string artisanGoodItemId, out string displayName))
                    continue;

                totalsByArtisanGood.TryGetValue(artisanGoodItemId, out (string DisplayName, int Quantity) existing);
                totalsByArtisanGood[artisanGoodItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByArtisanGood)
                TrackArtisanGoodSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetArtisanGoodInfo(Item? item, out string artisanGoodItemId, out string displayName)
        {
            artisanGoodItemId = string.Empty;
            displayName = string.Empty;

            if (!ArtisanGoodEconomyItemRules.TryGetArtisanGoodObject(item, out SObject artisanGoodObject))
                return false;

            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodObject.ItemId, out artisanGoodItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(artisanGoodObject.DisplayName)
                ? artisanGoodObject.Name
                : artisanGoodObject.DisplayName;

            return true;
        }

        public static bool TryResolveArtisanGoodItemId(string? rawInput, out string artisanGoodItemId, out string displayName)
        {
            artisanGoodItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (ArtisanGoodEconomyItemRules.TryCreateArtisanGoodObject(normalizedInput, out SObject? directObject))
                return TryGetArtisanGoodInfo(directObject, out artisanGoodItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!ArtisanGoodEconomyItemRules.TryCreateArtisanGoodObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetArtisanGoodInfo(namedObject, out artisanGoodItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeArtisanGoodItemId(string? rawArtisanGoodItemId, out string normalizedArtisanGoodItemId)
        {
            return ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(rawArtisanGoodItemId, out normalizedArtisanGoodItemId);
        }

        public static string GetArtisanGoodDisplayName(string? artisanGoodItemId)
        {
            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return artisanGoodItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedArtisanGoodItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedArtisanGoodItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
