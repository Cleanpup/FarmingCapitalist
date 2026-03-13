using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared crop-produce tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class CropSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (!TryGetCropProduceInfo(item, out string produceItemId, out string displayName))
                return;

            TrackProduceSale(produceItemId, displayName, quantity, source);
        }

        public static void TrackProduceSale(string produceItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (!TryNormalizeCropProduceItemId(produceItemId, out string normalizedProduceItemId))
                return;

            CropSupplyDataService.AddSupply(normalizedProduceItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            Dictionary<string, (string DisplayName, int Quantity)> totalsByCrop = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetCropProduceInfo(item, out string produceItemId, out string displayName))
                    continue;

                totalsByCrop.TryGetValue(produceItemId, out (string DisplayName, int Quantity) existing);
                totalsByCrop[produceItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByCrop)
            {
                TrackProduceSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
            }
        }

        public static bool TryGetCropProduceInfo(Item? item, out string produceItemId, out string displayName)
        {
            produceItemId = string.Empty;
            displayName = string.Empty;

            if (item is not SObject obj)
                return false;

            if (string.IsNullOrWhiteSpace(obj.ItemId) || !ItemCategoryRules.IsProduce(obj))
                return false;

            if (!CropTraitService.TryGetCropData(obj, out _, out _))
                return false;

            if (!TryNormalizeCropProduceItemId(obj.ItemId, out produceItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(obj.DisplayName)
                ? obj.Name
                : obj.DisplayName;

            return true;
        }

        public static bool TryResolveCropProduceItemId(string? rawInput, out string produceItemId, out string displayName)
        {
            produceItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (TryCreateCropProduceObject(normalizedInput, out SObject? directObject))
                return TryGetCropProduceInfo(directObject, out produceItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!TryCreateCropProduceObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetCropProduceInfo(namedObject, out produceItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeCropProduceItemId(string? rawProduceItemId, out string normalizedProduceItemId)
        {
            normalizedProduceItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawProduceItemId))
                return false;

            string candidate = rawProduceItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedProduceItemId = candidate;
            return true;
        }

        public static string GetCropDisplayName(string? cropProduceItemId)
        {
            if (!TryNormalizeCropProduceItemId(cropProduceItemId, out string normalizedProduceItemId))
                return cropProduceItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedProduceItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedProduceItemId;
        }

        private static bool TryCreateCropProduceObject(string rawItemId, out SObject? obj)
        {
            obj = null;

            if (!TryNormalizeCropProduceItemId(rawItemId, out string normalizedProduceItemId))
                return false;

            obj = ItemRegistry.Create<SObject>("(O)" + normalizedProduceItemId, allowNull: true);
            return obj is not null;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
