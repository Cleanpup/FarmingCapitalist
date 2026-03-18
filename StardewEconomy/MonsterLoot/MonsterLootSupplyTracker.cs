using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared monster-loot tracking helpers used by sale hooks and debug commands.
    /// </summary>
    internal static class MonsterLootSupplyTracker
    {
        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (MonsterLootSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetMonsterLootInfo(item, out string monsterLootItemId, out string displayName))
                return;

            TrackMonsterLootSale(monsterLootItemId, displayName, quantity, source);
        }

        public static void TrackMonsterLootSale(string monsterLootItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (MonsterLootSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return;

            if (!MonsterLootEconomyItemRules.IsMonsterLootItemId(normalizedMonsterLootItemId))
                return;

            MonsterLootSupplyDataService.AddSupply(normalizedMonsterLootItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (MonsterLootSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByMonsterLoot = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetMonsterLootInfo(item, out string monsterLootItemId, out string displayName))
                    continue;

                totalsByMonsterLoot.TryGetValue(monsterLootItemId, out (string DisplayName, int Quantity) existing);
                totalsByMonsterLoot[monsterLootItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByMonsterLoot)
                TrackMonsterLootSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
        }

        public static bool TryGetMonsterLootInfo(Item? item, out string monsterLootItemId, out string displayName)
        {
            monsterLootItemId = string.Empty;
            displayName = string.Empty;

            if (!MonsterLootEconomyItemRules.TryGetMonsterLootObject(item, out SObject monsterLootObject))
                return false;

            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootObject.ItemId, out monsterLootItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(monsterLootObject.DisplayName)
                ? monsterLootObject.Name
                : monsterLootObject.DisplayName;

            return true;
        }

        public static bool TryResolveMonsterLootItemId(string? rawInput, out string monsterLootItemId, out string displayName)
        {
            monsterLootItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (MonsterLootEconomyItemRules.TryCreateMonsterLootObject(normalizedInput, out SObject? directObject))
                return TryGetMonsterLootInfo(directObject, out monsterLootItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!MonsterLootEconomyItemRules.TryCreateMonsterLootObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetMonsterLootInfo(namedObject, out monsterLootItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeMonsterLootItemId(string? rawMonsterLootItemId, out string normalizedMonsterLootItemId)
        {
            return MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(rawMonsterLootItemId, out normalizedMonsterLootItemId);
        }

        public static string GetMonsterLootDisplayName(string? monsterLootItemId)
        {
            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return monsterLootItemId?.Trim() ?? string.Empty;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedMonsterLootItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedMonsterLootItemId;
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
