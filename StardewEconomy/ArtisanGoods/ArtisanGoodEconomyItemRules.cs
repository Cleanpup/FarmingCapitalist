using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Artisan-good-owned eligibility and normalization helpers.
    /// Membership intentionally stays curated to the requested artisan subcategories instead of every vanilla artisan-category output.
    /// </summary>
    internal static class ArtisanGoodEconomyItemRules
    {
        private static readonly HashSet<string> AlcoholBeverageItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "303", // Pale Ale
            "346", // Beer
            "348", // Wine
            "350", // Juice
            "395", // Coffee
            "459", // Mead
            "614"  // Green Tea
        };

        private static readonly HashSet<string> PreserveItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "342", // Pickles
            "344"  // Jelly
        };

        private static readonly HashSet<string> DairyArtisanGoodItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "424", // Cheese
            "426"  // Goat Cheese
        };

        private static readonly HashSet<string> ClothLoomProductItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "428" // Cloth
        };

        private static readonly HashSet<string> OilProductItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "247", // Oil
            "432"  // Truffle Oil
        };

        private static readonly HashSet<string> SpecialtyProcessedGoodItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "306", // Mayonnaise
            "307", // Duck Mayonnaise
            "308", // Void Mayonnaise
            "340", // Honey
            "445", // Caviar
            "447", // Aged Roe
            "807"  // Dinosaur Mayonnaise
        };

        public static bool IsArtisanGoodEligible(Item? item)
        {
            return TryGetArtisanGoodObject(item, out _);
        }

        public static bool TryGetArtisanGoodObject(Item? item, out SObject artisanGoodObject)
        {
            artisanGoodObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!TryNormalizeArtisanGoodItemId(obj.ItemId, out string normalizedItemId))
                return false;

            if (!IsArtisanGoodItemId(normalizedItemId))
                return false;

            artisanGoodObject = obj;
            return true;
        }

        public static bool TryNormalizeArtisanGoodItemId(string? rawArtisanGoodItemId, out string normalizedArtisanGoodItemId)
        {
            normalizedArtisanGoodItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawArtisanGoodItemId))
                return false;

            string candidate = rawArtisanGoodItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedArtisanGoodItemId = candidate;
            return true;
        }

        public static bool TryCreateArtisanGoodObject(string rawItemId, out SObject? artisanGoodObject)
        {
            artisanGoodObject = null;
            if (!TryNormalizeArtisanGoodItemId(rawItemId, out string normalizedArtisanGoodItemId))
                return false;

            artisanGoodObject = ItemRegistry.Create<SObject>("(O)" + normalizedArtisanGoodItemId, allowNull: true);
            return artisanGoodObject is not null
                && IsArtisanGoodItemId(normalizedArtisanGoodItemId);
        }

        public static bool IsAlcoholBeverage(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && AlcoholBeverageItemIds.Contains(itemId);
        }

        public static bool IsPreserve(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && PreserveItemIds.Contains(itemId);
        }

        public static bool IsDairyArtisanGood(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && DairyArtisanGoodItemIds.Contains(itemId);
        }

        public static bool IsClothLoomProduct(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && ClothLoomProductItemIds.Contains(itemId);
        }

        public static bool IsOilProduct(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && OilProductItemIds.Contains(itemId);
        }

        public static bool IsSpecialtyProcessedGood(Item? item)
        {
            return TryGetNormalizedArtisanGoodItemId(item, out string itemId)
                && SpecialtyProcessedGoodItemIds.Contains(itemId);
        }

        public static bool IsArtisanGoodItemId(string? artisanGoodItemId)
        {
            return TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId)
                && (AlcoholBeverageItemIds.Contains(normalizedArtisanGoodItemId)
                    || PreserveItemIds.Contains(normalizedArtisanGoodItemId)
                    || DairyArtisanGoodItemIds.Contains(normalizedArtisanGoodItemId)
                    || ClothLoomProductItemIds.Contains(normalizedArtisanGoodItemId)
                    || OilProductItemIds.Contains(normalizedArtisanGoodItemId)
                    || SpecialtyProcessedGoodItemIds.Contains(normalizedArtisanGoodItemId));
        }

        private static bool TryGetNormalizedArtisanGoodItemId(Item? item, out string artisanGoodItemId)
        {
            artisanGoodItemId = string.Empty;
            return item is SObject obj
                && TryNormalizeArtisanGoodItemId(obj.ItemId, out artisanGoodItemId)
                && IsArtisanGoodItemId(artisanGoodItemId);
        }
    }
}
