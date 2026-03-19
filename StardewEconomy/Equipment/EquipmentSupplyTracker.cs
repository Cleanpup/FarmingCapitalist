using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared equipment tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class EquipmentSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (EquipmentSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetEquipmentInfo(item, out string equipmentItemId, out string displayName))
                return;

            TrackEquipmentSale(equipmentItemId, displayName, quantity, source);
        }

        public static void TrackEquipmentSale(string equipmentItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (EquipmentSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId))
                return;

            if (!EquipmentEconomyItemRules.IsEquipmentItemId(normalizedEquipmentItemId))
                return;

            EquipmentSupplyDataService.AddSupply(normalizedEquipmentItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (EquipmentSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByEquipment = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetEquipmentInfo(item, out string equipmentItemId, out string displayName))
                    continue;

                totalsByEquipment.TryGetValue(equipmentItemId, out (string DisplayName, int Quantity) existing);
                totalsByEquipment[equipmentItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByEquipment)
                TrackEquipmentSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetEquipmentInfo(Item? item, out string equipmentItemId, out string displayName)
        {
            equipmentItemId = string.Empty;
            displayName = string.Empty;

            if (!EquipmentEconomyItemRules.TryGetEquipmentItem(item, out Item equipmentItem))
                return false;

            equipmentItemId = equipmentItem.QualifiedItemId;
            displayName = equipmentItem.DisplayName;
            return !string.IsNullOrWhiteSpace(equipmentItemId);
        }

        public static bool TryResolveEquipmentItemId(string? rawInput, out string equipmentItemId, out string displayName)
        {
            equipmentItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (EquipmentEconomyItemRules.TryCreateEquipmentItem(normalizedInput, out Item? directItem) && directItem is not null)
                return TryGetEquipmentInfo(directItem, out equipmentItemId, out displayName);

            List<Item> matches = new();
            foreach (string candidateEquipmentItemId in GetKnownEquipmentItemIds())
            {
                Item? candidateItem = ItemRegistry.Create(candidateEquipmentItemId, allowNull: true);
                if (candidateItem is null || !MatchesInput(candidateItem, normalizedInput))
                    continue;

                matches.Add(candidateItem);
            }

            return matches.Count == 1
                && TryGetEquipmentInfo(matches[0], out equipmentItemId, out displayName);
        }

        public static bool TryNormalizeEquipmentItemId(string? rawEquipmentItemId, out string normalizedEquipmentItemId)
        {
            return EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(rawEquipmentItemId, out normalizedEquipmentItemId);
        }

        public static string GetEquipmentDisplayName(string? equipmentItemId)
        {
            if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId))
                return equipmentItemId?.Trim() ?? string.Empty;

            string? displayName = ItemRegistry.GetData(normalizedEquipmentItemId)?.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return normalizedEquipmentItemId;
        }

        private static IEnumerable<string> GetKnownEquipmentItemIds()
        {
            foreach (string weaponItemId in Game1.weaponData.Keys)
            {
                string qualifiedItemId = ItemRegistry.ManuallyQualifyItemId(weaponItemId, ItemRegistry.type_weapon);
                if (EquipmentEconomyItemRules.IsEquipmentItemId(qualifiedItemId))
                    yield return qualifiedItemId;
            }

            foreach (KeyValuePair<string, StardewValley.GameData.Objects.ObjectData> pair in Game1.objectData)
            {
                if (pair.Value.Category != -96)
                    continue;

                string qualifiedItemId = ItemRegistry.ManuallyQualifyItemId(pair.Key, ItemRegistry.type_object);
                if (EquipmentEconomyItemRules.IsEquipmentItemId(qualifiedItemId))
                    yield return qualifiedItemId;
            }

            foreach (string bootsItemId in DataLoader.Boots(Game1.content).Keys)
            {
                string qualifiedItemId = ItemRegistry.ManuallyQualifyItemId(bootsItemId, ItemRegistry.type_boots);
                if (EquipmentEconomyItemRules.IsEquipmentItemId(qualifiedItemId))
                    yield return qualifiedItemId;
            }
        }

        private static bool MatchesInput(Item item, string input)
        {
            return string.Equals(item.QualifiedItemId, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ItemId, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
