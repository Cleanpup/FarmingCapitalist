using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared forageable tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class ForageableSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (ForageableSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetForageableInfo(item, out string forageableItemId, out string displayName))
                return;

            TrackForageableSale(forageableItemId, displayName, quantity, source);
        }

        public static void TrackForageableSale(string forageableItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (ForageableSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return;

            if (!ForageableEconomyItemRules.IsForageableItemId(normalizedForageableItemId))
                return;

            ForageableSupplyDataService.AddSupply(normalizedForageableItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (ForageableSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByForageable = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetForageableInfo(item, out string forageableItemId, out string displayName))
                    continue;

                totalsByForageable.TryGetValue(forageableItemId, out (string DisplayName, int Quantity) existing);
                totalsByForageable[forageableItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByForageable)
                TrackForageableSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetForageableInfo(Item? item, out string forageableItemId, out string displayName)
        {
            forageableItemId = string.Empty;
            displayName = string.Empty;

            if (!ForageableEconomyItemRules.TryGetForageableObject(item, out SObject forageableObject))
                return false;

            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableObject.ItemId, out forageableItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(forageableObject.DisplayName)
                ? forageableObject.Name
                : forageableObject.DisplayName;

            return true;
        }

        public static bool TryResolveForageableItemId(string? rawInput, out string forageableItemId, out string displayName)
        {
            forageableItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (ForageableEconomyItemRules.TryCreateForageableObject(normalizedInput, out SObject? directObject))
                return TryGetForageableInfo(directObject, out forageableItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!ForageableEconomyItemRules.TryCreateForageableObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetForageableInfo(namedObject, out forageableItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeForageableItemId(string? rawForageableItemId, out string normalizedForageableItemId)
        {
            return ForageableEconomyItemRules.TryNormalizeForageableItemId(rawForageableItemId, out normalizedForageableItemId);
        }

        public static string GetForageableDisplayName(string? forageableItemId)
        {
            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return forageableItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedForageableItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedForageableItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
