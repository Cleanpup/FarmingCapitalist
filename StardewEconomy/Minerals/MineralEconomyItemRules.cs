using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mining-owned eligibility and normalization helpers. Base category predicates remain in ItemCategoryRules.
    /// The owning module still uses Mineral* type names for save compatibility, but it now covers the broader mining bucket.
    /// </summary>
    internal static class MineralEconomyItemRules
    {
        public static bool IsMineralEconomyEligible(Item? item)
        {
            return TryGetMineralEconomyObject(item, out _);
        }

        public static bool TryGetMineralEconomyObject(Item? item, out SObject mineralObject)
        {
            mineralObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!ItemCategoryRules.IsMiningMaterial(obj))
                return false;

            mineralObject = obj;
            return true;
        }

        public static bool TryNormalizeMineralItemId(string? rawMineralItemId, out string normalizedMineralItemId)
        {
            normalizedMineralItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawMineralItemId))
                return false;

            string candidate = rawMineralItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedMineralItemId = candidate;
            return true;
        }

        public static bool TryCreateMineralObject(string rawItemId, out SObject? mineralObject)
        {
            mineralObject = null;

            if (!TryNormalizeMineralItemId(rawItemId, out string normalizedMineralItemId))
                return false;

            mineralObject = ItemRegistry.Create<SObject>("(O)" + normalizedMineralItemId, allowNull: true);
            return mineralObject is not null && ItemCategoryRules.IsMiningMaterial(mineralObject);
        }
    }
}
