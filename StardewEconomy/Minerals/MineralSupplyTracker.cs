using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared mineral tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class MineralSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (MineralSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetMineralInfo(item, out string mineralItemId, out string displayName))
                return;

            TrackMineralSale(mineralItemId, displayName, quantity, source);
        }

        public static void TrackMineralSale(string mineralItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (MineralSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return;

            MineralSupplyDataService.AddSupply(normalizedMineralItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (MineralSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByMineral = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetMineralInfo(item, out string mineralItemId, out string displayName))
                    continue;

                totalsByMineral.TryGetValue(mineralItemId, out (string DisplayName, int Quantity) existing);
                totalsByMineral[mineralItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByMineral)
            {
                TrackMineralSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
            }
        }

        public static bool TryGetMineralInfo(Item? item, out string mineralItemId, out string displayName)
        {
            mineralItemId = string.Empty;
            displayName = string.Empty;

            if (!MineralEconomyItemRules.TryGetMineralEconomyObject(item, out SObject mineralObject))
                return false;

            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralObject.ItemId, out mineralItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(mineralObject.DisplayName)
                ? mineralObject.Name
                : mineralObject.DisplayName;

            return true;
        }

        public static bool TryResolveMineralItemId(string? rawInput, out string mineralItemId, out string displayName)
        {
            mineralItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (MineralEconomyItemRules.TryCreateMineralObject(normalizedInput, out SObject? directObject))
                return TryGetMineralInfo(directObject, out mineralItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!MineralEconomyItemRules.TryCreateMineralObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetMineralInfo(namedObject, out mineralItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeMineralItemId(string? rawMineralItemId, out string normalizedMineralItemId)
        {
            return MineralEconomyItemRules.TryNormalizeMineralItemId(rawMineralItemId, out normalizedMineralItemId);
        }

        public static string GetMineralDisplayName(string? mineralItemId)
        {
            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return mineralItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedMineralItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedMineralItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
