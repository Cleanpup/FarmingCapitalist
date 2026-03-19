using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared crafting-extra tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class CraftingExtraSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (CraftingExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetCraftingExtraInfo(item, out string craftingExtraItemId, out string displayName))
                return;

            TrackCraftingExtraSale(craftingExtraItemId, displayName, quantity, source);
        }

        public static void TrackCraftingExtraSale(string craftingExtraItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (CraftingExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return;

            if (!CraftingExtraEconomyItemRules.IsCraftingExtraItemId(normalizedCraftingExtraItemId))
                return;

            CraftingExtraSupplyDataService.AddSupply(normalizedCraftingExtraItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (CraftingExtraSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByCraftingExtra = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetCraftingExtraInfo(item, out string craftingExtraItemId, out string displayName))
                    continue;

                totalsByCraftingExtra.TryGetValue(craftingExtraItemId, out (string DisplayName, int Quantity) existing);
                totalsByCraftingExtra[craftingExtraItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByCraftingExtra)
                TrackCraftingExtraSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetCraftingExtraInfo(Item? item, out string craftingExtraItemId, out string displayName)
        {
            craftingExtraItemId = string.Empty;
            displayName = string.Empty;

            if (!CraftingExtraEconomyItemRules.TryGetCraftingExtraObject(item, out SObject craftingExtraObject))
                return false;

            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraObject.ItemId, out craftingExtraItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(craftingExtraObject.DisplayName)
                ? craftingExtraObject.Name
                : craftingExtraObject.DisplayName;

            return true;
        }

        public static bool TryResolveCraftingExtraItemId(string? rawInput, out string craftingExtraItemId, out string displayName)
        {
            craftingExtraItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (CraftingExtraEconomyItemRules.TryCreateCraftingExtraObject(normalizedInput, out SObject? directObject))
                return TryGetCraftingExtraInfo(directObject, out craftingExtraItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!CraftingExtraEconomyItemRules.TryCreateCraftingExtraObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetCraftingExtraInfo(namedObject, out craftingExtraItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeCraftingExtraItemId(string? rawCraftingExtraItemId, out string normalizedCraftingExtraItemId)
        {
            return CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(rawCraftingExtraItemId, out normalizedCraftingExtraItemId);
        }

        public static string GetCraftingExtraDisplayName(string? craftingExtraItemId)
        {
            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return craftingExtraItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedCraftingExtraItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedCraftingExtraItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
