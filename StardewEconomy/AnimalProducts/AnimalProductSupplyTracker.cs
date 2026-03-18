using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared animal product tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class AnimalProductSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (AnimalProductSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetAnimalProductInfo(item, out string animalProductItemId, out string displayName))
                return;

            TrackAnimalProductSale(animalProductItemId, displayName, quantity, source);
        }

        public static void TrackAnimalProductSale(string animalProductItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (AnimalProductSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return;

            if (!AnimalProductEconomyItemRules.IsAnimalProductItemId(normalizedAnimalProductItemId))
                return;

            AnimalProductSupplyDataService.AddSupply(normalizedAnimalProductItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (AnimalProductSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByAnimalProduct = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetAnimalProductInfo(item, out string animalProductItemId, out string displayName))
                    continue;

                totalsByAnimalProduct.TryGetValue(animalProductItemId, out (string DisplayName, int Quantity) existing);
                totalsByAnimalProduct[animalProductItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByAnimalProduct)
                TrackAnimalProductSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetAnimalProductInfo(Item? item, out string animalProductItemId, out string displayName)
        {
            animalProductItemId = string.Empty;
            displayName = string.Empty;

            if (!AnimalProductEconomyItemRules.TryGetAnimalProductObject(item, out SObject animalProductObject))
                return false;

            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductObject.ItemId, out animalProductItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(animalProductObject.DisplayName)
                ? animalProductObject.Name
                : animalProductObject.DisplayName;

            return true;
        }

        public static bool TryResolveAnimalProductItemId(string? rawInput, out string animalProductItemId, out string displayName)
        {
            animalProductItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (AnimalProductEconomyItemRules.TryCreateAnimalProductObject(normalizedInput, out SObject? directObject))
                return TryGetAnimalProductInfo(directObject, out animalProductItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!AnimalProductEconomyItemRules.TryCreateAnimalProductObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetAnimalProductInfo(namedObject, out animalProductItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeAnimalProductItemId(string? rawAnimalProductItemId, out string normalizedAnimalProductItemId)
        {
            return AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(rawAnimalProductItemId, out normalizedAnimalProductItemId);
        }

        public static string GetAnimalProductDisplayName(string? animalProductItemId)
        {
            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return animalProductItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedAnimalProductItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedAnimalProductItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
