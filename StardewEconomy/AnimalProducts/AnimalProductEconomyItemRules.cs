using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Animal-product-owned eligibility and normalization helpers.
    /// Membership intentionally stays narrow to direct animal outputs and excludes artisan goods.
    /// </summary>
    internal static class AnimalProductEconomyItemRules
    {
        private const string TruffleItemId = "430";

        private static readonly HashSet<string> EggItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "107", // Dinosaur Egg
            "174", // Large Egg
            "176", // Egg
            "180", // Brown Egg
            "182", // Large Brown Egg
            "289", // Ostrich Egg
            "305", // Void Egg
            "442", // Duck Egg
            "928"  // Golden Egg
        };

        private static readonly HashSet<string> MilkItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "184", // Milk
            "186", // Large Milk
            "436", // Goat Milk
            "438"  // Large Goat Milk
        };

        private static readonly HashSet<string> FiberAnimalProductItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "440" // Wool
        };

        private static readonly HashSet<string> CoopProductItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "107", // Dinosaur Egg
            "174", // Large Egg
            "176", // Egg
            "180", // Brown Egg
            "182", // Large Brown Egg
            "289", // Ostrich Egg
            "305", // Void Egg
            "440", // Wool (rabbit)
            "442", // Duck Egg
            "444", // Duck Feather
            "446", // Rabbit's Foot
            "928"  // Golden Egg
        };

        private static readonly HashSet<string> BarnProductItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "184", // Milk
            "186", // Large Milk
            "430", // Truffle
            "436", // Goat Milk
            "438", // Large Goat Milk
            "440"  // Wool (sheep)
        };

        private static readonly HashSet<string> SpecialtyAnimalGoodItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "107", // Dinosaur Egg
            "289", // Ostrich Egg
            "305", // Void Egg
            "430", // Truffle
            "444", // Duck Feather
            "446", // Rabbit's Foot
            "928"  // Golden Egg
        };

        public static bool IsAnimalProductEligible(Item? item)
        {
            return TryGetAnimalProductObject(item, out _);
        }

        public static bool TryGetAnimalProductObject(Item? item, out SObject animalProductObject)
        {
            animalProductObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!TryNormalizeAnimalProductItemId(obj.ItemId, out string normalizedItemId))
                return false;

            if (!IsAnimalProductItemId(normalizedItemId))
                return false;

            animalProductObject = obj;
            return true;
        }

        public static bool TryNormalizeAnimalProductItemId(string? rawAnimalProductItemId, out string normalizedAnimalProductItemId)
        {
            normalizedAnimalProductItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawAnimalProductItemId))
                return false;

            string candidate = rawAnimalProductItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedAnimalProductItemId = candidate;
            return true;
        }

        public static bool TryCreateAnimalProductObject(string rawItemId, out SObject? animalProductObject)
        {
            animalProductObject = null;
            if (!TryNormalizeAnimalProductItemId(rawItemId, out string normalizedAnimalProductItemId))
                return false;

            animalProductObject = ItemRegistry.Create<SObject>("(O)" + normalizedAnimalProductItemId, allowNull: true);
            return animalProductObject is not null
                && IsAnimalProductItemId(normalizedAnimalProductItemId);
        }

        public static bool IsEggProduct(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && EggItemIds.Contains(itemId);
        }

        public static bool IsMilkProduct(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && MilkItemIds.Contains(itemId);
        }

        public static bool IsFiberAnimalProduct(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && FiberAnimalProductItemIds.Contains(itemId);
        }

        public static bool IsCoopProduct(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && CoopProductItemIds.Contains(itemId);
        }

        public static bool IsBarnProduct(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && BarnProductItemIds.Contains(itemId);
        }

        public static bool IsSpecialtyAnimalGood(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && SpecialtyAnimalGoodItemIds.Contains(itemId);
        }

        public static bool IsSeasonalTruffle(Item? item)
        {
            return TryGetNormalizedAnimalProductItemId(item, out string itemId)
                && string.Equals(itemId, TruffleItemId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSeasonalTruffle(string? animalProductItemId)
        {
            return TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId)
                && string.Equals(normalizedAnimalProductItemId, TruffleItemId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAnimalProductItemId(string? animalProductItemId)
        {
            return TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId)
                && (EggItemIds.Contains(normalizedAnimalProductItemId)
                    || MilkItemIds.Contains(normalizedAnimalProductItemId)
                    || FiberAnimalProductItemIds.Contains(normalizedAnimalProductItemId)
                    || CoopProductItemIds.Contains(normalizedAnimalProductItemId)
                    || BarnProductItemIds.Contains(normalizedAnimalProductItemId)
                    || SpecialtyAnimalGoodItemIds.Contains(normalizedAnimalProductItemId));
        }

        private static bool TryGetNormalizedAnimalProductItemId(Item? item, out string animalProductItemId)
        {
            animalProductItemId = string.Empty;
            return item is SObject obj
                && TryNormalizeAnimalProductItemId(obj.ItemId, out animalProductItemId)
                && IsAnimalProductItemId(animalProductItemId);
        }
    }
}
