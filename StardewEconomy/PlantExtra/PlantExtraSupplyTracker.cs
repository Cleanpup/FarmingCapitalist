using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared plantExtra tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class PlantExtraSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (PlantExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetPlantExtraInfo(item, out string plantExtraItemId, out string displayName))
                return;

            TrackPlantExtraSale(plantExtraItemId, displayName, quantity, source);
        }

        public static void TrackPlantExtraSale(string plantExtraItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (PlantExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return;

            if (!PlantExtraEconomyItemRules.IsPlantExtraItemId(normalizedPlantExtraItemId))
                return;

            PlantExtraSupplyDataService.AddSupply(normalizedPlantExtraItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (PlantExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByPlantExtra = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetPlantExtraInfo(item, out string plantExtraItemId, out string displayName))
                    continue;

                totalsByPlantExtra.TryGetValue(plantExtraItemId, out (string DisplayName, int Quantity) existing);
                totalsByPlantExtra[plantExtraItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByPlantExtra)
                TrackPlantExtraSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetPlantExtraInfo(Item? item, out string plantExtraItemId, out string displayName)
        {
            plantExtraItemId = string.Empty;
            displayName = string.Empty;

            if (!PlantExtraEconomyItemRules.TryGetPlantExtraObject(item, out SObject plantExtraObject))
                return false;

            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraObject.ItemId, out plantExtraItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(plantExtraObject.DisplayName)
                ? plantExtraObject.Name
                : plantExtraObject.DisplayName;

            return true;
        }

        public static bool TryResolvePlantExtraItemId(string? rawInput, out string plantExtraItemId, out string displayName)
        {
            plantExtraItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (PlantExtraEconomyItemRules.TryCreatePlantExtraObject(normalizedInput, out SObject? directObject))
                return TryGetPlantExtraInfo(directObject, out plantExtraItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!PlantExtraEconomyItemRules.TryCreatePlantExtraObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetPlantExtraInfo(namedObject, out plantExtraItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizePlantExtraItemId(string? rawPlantExtraItemId, out string normalizedPlantExtraItemId)
        {
            return PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(rawPlantExtraItemId, out normalizedPlantExtraItemId);
        }

        public static string GetPlantExtraDisplayName(string? plantExtraItemId)
        {
            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return plantExtraItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedPlantExtraItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedPlantExtraItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
