using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared cooking-food tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class CookingFoodSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (CookingFoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetCookingFoodInfo(item, out string cookingFoodItemId, out string displayName))
                return;

            TrackCookingFoodSale(cookingFoodItemId, displayName, quantity, source);
        }

        public static void TrackCookingFoodSale(string cookingFoodItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (CookingFoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return;

            if (!CookingFoodEconomyItemRules.IsCookingFoodItemId(normalizedCookingFoodItemId))
                return;

            CookingFoodSupplyDataService.AddSupply(normalizedCookingFoodItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (CookingFoodSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByCookingFood = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetCookingFoodInfo(item, out string cookingFoodItemId, out string displayName))
                    continue;

                totalsByCookingFood.TryGetValue(cookingFoodItemId, out (string DisplayName, int Quantity) existing);
                totalsByCookingFood[cookingFoodItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByCookingFood)
                TrackCookingFoodSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetCookingFoodInfo(Item? item, out string cookingFoodItemId, out string displayName)
        {
            cookingFoodItemId = string.Empty;
            displayName = string.Empty;

            if (!CookingFoodEconomyItemRules.TryGetCookingFoodObject(item, out SObject cookingFoodObject))
                return false;

            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodObject.ItemId, out cookingFoodItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(cookingFoodObject.DisplayName)
                ? cookingFoodObject.Name
                : cookingFoodObject.DisplayName;

            return true;
        }

        public static bool TryResolveCookingFoodItemId(string? rawInput, out string cookingFoodItemId, out string displayName)
        {
            cookingFoodItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (CookingFoodEconomyItemRules.TryCreateCookingFoodObject(normalizedInput, out SObject? directObject))
                return TryGetCookingFoodInfo(directObject, out cookingFoodItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!CookingFoodEconomyItemRules.TryCreateCookingFoodObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetCookingFoodInfo(namedObject, out cookingFoodItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeCookingFoodItemId(string? rawCookingFoodItemId, out string normalizedCookingFoodItemId)
        {
            return CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(rawCookingFoodItemId, out normalizedCookingFoodItemId);
        }

        public static string GetCookingFoodDisplayName(string? cookingFoodItemId)
        {
            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return cookingFoodItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedCookingFoodItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedCookingFoodItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
