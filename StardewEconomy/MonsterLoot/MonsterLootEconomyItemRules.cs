using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Monster-loot-owned eligibility and normalization helpers.
    /// Membership intentionally stays curated to the requested monster-loot subcategories instead of every vanilla loot-like item.
    /// </summary>
    internal static class MonsterLootEconomyItemRules
    {
        private static readonly HashSet<string> BasicMonsterDropItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "684", // Bug Meat
            "767", // Bat Wing
            "881"  // Bone Fragment
        };

        private static readonly HashSet<string> SlimeRelatedItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "413", // Blue Slime Egg
            "437", // Red Slime Egg
            "439", // Purple Slime Egg
            "557", // Petrified Slime
            "680", // Green Slime Egg
            "766", // Slime
            "857"  // Tiger Slime Egg
        };

        private static readonly HashSet<string> EssenceMagicalDropItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "768", // Solar Essence
            "769"  // Void Essence
        };

        public static bool IsMonsterLootEligible(Item? item)
        {
            return TryGetMonsterLootObject(item, out _);
        }

        public static bool TryGetMonsterLootObject(Item? item, out SObject monsterLootObject)
        {
            monsterLootObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!TryNormalizeMonsterLootItemId(obj.ItemId, out string normalizedItemId))
                return false;

            if (!IsMonsterLootItemId(normalizedItemId))
                return false;

            monsterLootObject = obj;
            return true;
        }

        public static bool TryNormalizeMonsterLootItemId(string? rawMonsterLootItemId, out string normalizedMonsterLootItemId)
        {
            normalizedMonsterLootItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawMonsterLootItemId))
                return false;

            string candidate = rawMonsterLootItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedMonsterLootItemId = candidate;
            return true;
        }

        public static bool TryCreateMonsterLootObject(string rawItemId, out SObject? monsterLootObject)
        {
            monsterLootObject = null;
            if (!TryNormalizeMonsterLootItemId(rawItemId, out string normalizedMonsterLootItemId))
                return false;

            monsterLootObject = ItemRegistry.Create<SObject>("(O)" + normalizedMonsterLootItemId, allowNull: true);
            return monsterLootObject is not null
                && IsMonsterLootItemId(normalizedMonsterLootItemId);
        }

        public static bool IsBasicMonsterDrop(Item? item)
        {
            return TryGetNormalizedMonsterLootItemId(item, out string itemId)
                && BasicMonsterDropItemIds.Contains(itemId);
        }

        public static bool IsSlimeRelatedItem(Item? item)
        {
            return TryGetNormalizedMonsterLootItemId(item, out string itemId)
                && SlimeRelatedItemIds.Contains(itemId);
        }

        public static bool IsEssenceMagicalDrop(Item? item)
        {
            return TryGetNormalizedMonsterLootItemId(item, out string itemId)
                && EssenceMagicalDropItemIds.Contains(itemId);
        }

        public static bool IsMonsterLootItemId(string? monsterLootItemId)
        {
            return TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId)
                && (BasicMonsterDropItemIds.Contains(normalizedMonsterLootItemId)
                    || SlimeRelatedItemIds.Contains(normalizedMonsterLootItemId)
                    || EssenceMagicalDropItemIds.Contains(normalizedMonsterLootItemId));
        }

        private static bool TryGetNormalizedMonsterLootItemId(Item? item, out string monsterLootItemId)
        {
            monsterLootItemId = string.Empty;
            return item is SObject obj
                && TryNormalizeMonsterLootItemId(obj.ItemId, out monsterLootItemId)
                && IsMonsterLootItemId(monsterLootItemId);
        }
    }
}
