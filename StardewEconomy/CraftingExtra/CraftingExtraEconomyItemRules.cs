using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Crafting-extra-owned eligibility and normalization helpers.
    /// Membership is intentionally hard-bounded to the requested sellable materials only.
    /// </summary>
    internal static class CraftingExtraEconomyItemRules
    {
        private static readonly HashSet<string> EligibleNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Wood",
            "Hardwood",
            "Stone",
            "Fiber",
            "Sap",
            "Clay",
            "Moss"
        };

        public static bool IsCraftingExtraEligible(Item? item)
        {
            return TryGetCraftingExtraObject(item, out _);
        }

        public static bool TryGetCraftingExtraObject(Item? item, out SObject craftingExtraObject)
        {
            craftingExtraObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (obj.salePrice() <= 0)
                return false;

            if (!MatchesCraftingExtraMembership(obj))
                return false;

            craftingExtraObject = obj;
            return true;
        }

        public static bool TryNormalizeCraftingExtraItemId(string? rawCraftingExtraItemId, out string normalizedCraftingExtraItemId)
        {
            normalizedCraftingExtraItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawCraftingExtraItemId))
                return false;

            string candidate = rawCraftingExtraItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedCraftingExtraItemId = candidate;
            return true;
        }

        public static bool TryCreateCraftingExtraObject(string rawItemId, out SObject? craftingExtraObject)
        {
            craftingExtraObject = null;
            if (!TryNormalizeCraftingExtraItemId(rawItemId, out string normalizedCraftingExtraItemId))
                return false;

            craftingExtraObject = ItemRegistry.Create<SObject>("(O)" + normalizedCraftingExtraItemId, allowNull: true);
            return craftingExtraObject is not null
                && TryGetCraftingExtraObject(craftingExtraObject, out _);
        }

        public static bool IsCraftingExtraItemId(string? craftingExtraItemId)
        {
            return TryCreateCraftingExtraObject(craftingExtraItemId ?? string.Empty, out _);
        }

        public static bool IsWood(Item? item)
        {
            return MatchesName(item, "Wood");
        }

        public static bool IsWood(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Wood");
        }

        public static bool IsHardwood(Item? item)
        {
            return MatchesName(item, "Hardwood");
        }

        public static bool IsHardwood(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Hardwood");
        }

        public static bool IsStone(Item? item)
        {
            return MatchesName(item, "Stone");
        }

        public static bool IsStone(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Stone");
        }

        public static bool IsFiber(Item? item)
        {
            return MatchesName(item, "Fiber");
        }

        public static bool IsFiber(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Fiber");
        }

        public static bool IsSap(Item? item)
        {
            return MatchesName(item, "Sap");
        }

        public static bool IsSap(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Sap");
        }

        public static bool IsClay(Item? item)
        {
            return MatchesName(item, "Clay");
        }

        public static bool IsClay(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Clay");
        }

        public static bool IsMoss(Item? item)
        {
            return MatchesName(item, "Moss");
        }

        public static bool IsMoss(string? craftingExtraItemId)
        {
            return MatchesName(craftingExtraItemId, "Moss");
        }

        private static bool MatchesCraftingExtraMembership(SObject obj)
        {
            foreach (string candidateName in GetKnownNames(obj))
            {
                if (EligibleNames.Contains(candidateName))
                    return true;
            }

            return false;
        }

        private static bool MatchesName(Item? item, string expectedName)
        {
            return TryGetCraftingExtraObject(item, out SObject craftingExtraObject)
                && MatchesKnownName(craftingExtraObject, expectedName);
        }

        private static bool MatchesName(string? craftingExtraItemId, string expectedName)
        {
            return TryCreateCraftingExtraObject(craftingExtraItemId ?? string.Empty, out SObject? craftingExtraObject)
                && craftingExtraObject is not null
                && MatchesKnownName(craftingExtraObject, expectedName);
        }

        private static bool MatchesKnownName(SObject craftingExtraObject, string expectedName)
        {
            foreach (string candidateName in GetKnownNames(craftingExtraObject))
            {
                if (string.Equals(candidateName, expectedName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> GetKnownNames(SObject obj)
        {
            if (!string.IsNullOrWhiteSpace(obj.Name))
                yield return obj.Name.Trim();

            if (!string.IsNullOrWhiteSpace(obj.DisplayName))
                yield return obj.DisplayName.Trim();
        }
    }
}
