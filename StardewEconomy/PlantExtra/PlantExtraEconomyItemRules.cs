using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Plant-extra-owned eligibility and normalization helpers.
    /// Membership stays explicit where this category overlaps crops, forageables, and cooking ingredients.
    /// </summary>
    internal static class PlantExtraEconomyItemRules
    {
        private static readonly IReadOnlyList<string> AllSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        private static readonly HashSet<string> TreeFruitNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Apple",
            "Apricot",
            "Banana",
            "Cherry",
            "Mango",
            "Orange",
            "Peach",
            "Pomegranate"
        };

        private static readonly HashSet<string> TreeSaplingNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Apple Sapling",
            "Apricot Sapling",
            "Banana Sapling",
            "Cherry Sapling",
            "Mango Sapling",
            "Orange Sapling",
            "Peach Sapling",
            "Pomegranate Sapling",
            "Tea Sapling"
        };

        private static readonly HashSet<string> TreeSeedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Acorn",
            "Maple Seed",
            "Pine Cone",
            "Mahogany Seed",
            "Mystic Tree Seed",
            "Mushroom Tree Seed"
        };

        private static readonly HashSet<string> FlowerNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Blue Jazz",
            "Fairy Rose",
            "Poppy",
            "Summer Spangle",
            "Sunflower",
            "Tulip"
        };

        private static readonly HashSet<string> FlowerSeedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fairy Seeds",
            "Jazz Seeds",
            "Poppy Seeds",
            "Spangle Seeds",
            "Sunflower Seeds",
            "Tulip Bulb"
        };

        private static readonly HashSet<string> SpecialSeedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ancient Seeds",
            "Fall Seeds",
            "Fiber Seeds",
            "Mixed Seeds",
            "Rare Seed",
            "Spring Seeds",
            "Summer Seeds",
            "Winter Seeds",
            "Wild Seeds (Fa)",
            "Wild Seeds (Sp)",
            "Wild Seeds (Su)",
            "Wild Seeds (Wi)"
        };

        private static readonly HashSet<string> MushroomNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Chanterelle",
            "Common Mushroom",
            "Magma Cap",
            "Morel",
            "Purple Mushroom",
            "Red Mushroom"
        };

        private static readonly HashSet<string> ExplicitTappedProductNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Maple Syrup",
            "Oak Resin",
            "Pine Tar",
            "Mystic Syrup"
        };

        private static readonly Dictionary<string, IReadOnlyList<string>> SeasonKeysByName =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Apricot"] = new[] { "spring" },
                ["Cherry"] = new[] { "spring" },
                ["Orange"] = new[] { "summer" },
                ["Peach"] = new[] { "summer" },
                ["Banana"] = new[] { "summer" },
                ["Mango"] = new[] { "summer" },
                ["Apple"] = new[] { "fall" },
                ["Pomegranate"] = new[] { "fall" },
                ["Blue Jazz"] = new[] { "spring" },
                ["Tulip"] = new[] { "spring" },
                ["Summer Spangle"] = new[] { "summer" },
                ["Poppy"] = new[] { "summer" },
                ["Sunflower"] = new[] { "summer", "fall" },
                ["Fairy Rose"] = new[] { "fall" }
            };

        public static bool IsPlantExtraEligible(Item? item)
        {
            return TryGetPlantExtraObject(item, out _);
        }

        public static bool TryGetPlantExtraObject(Item? item, out SObject plantExtraObject)
        {
            plantExtraObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (obj.salePrice() <= 0)
                return false;

            if (!MatchesPlantExtraMembership(obj))
                return false;

            plantExtraObject = obj;
            return true;
        }

        public static bool TryNormalizePlantExtraItemId(string? rawPlantExtraItemId, out string normalizedPlantExtraItemId)
        {
            normalizedPlantExtraItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPlantExtraItemId))
                return false;

            string candidate = rawPlantExtraItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedPlantExtraItemId = candidate;
            return true;
        }

        public static bool TryCreatePlantExtraObject(string rawItemId, out SObject? plantExtraObject)
        {
            plantExtraObject = null;
            if (!TryNormalizePlantExtraItemId(rawItemId, out string normalizedPlantExtraItemId))
                return false;

            plantExtraObject = ItemRegistry.Create<SObject>("(O)" + normalizedPlantExtraItemId, allowNull: true);
            return plantExtraObject is not null
                && TryGetPlantExtraObject(plantExtraObject, out _);
        }

        public static bool IsPlantExtraItemId(string? plantExtraItemId)
        {
            return TryCreatePlantExtraObject(plantExtraItemId ?? string.Empty, out _);
        }

        public static bool IsTreeFruit(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && MatchesAnyKnownName(obj, TreeFruitNames);
        }

        public static bool IsTreeSapling(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && (MatchesAnyKnownName(obj, TreeSaplingNames) || MatchesAnyKnownName(obj, TreeSeedNames));
        }

        public static bool IsFlower(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && ItemCategoryRules.IsFlower(obj)
                && MatchesAnyKnownName(obj, FlowerNames);
        }

        public static bool IsFlowerSeedSpecialSeed(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && (MatchesAnyKnownName(obj, FlowerSeedNames) || MatchesAnyKnownName(obj, SpecialSeedNames));
        }

        public static bool IsMushroom(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && MatchesAnyKnownName(obj, MushroomNames);
        }

        public static bool IsTappedProduct(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && (ItemCategoryRules.IsSyrup(obj) || MatchesAnyKnownName(obj, ExplicitTappedProductNames));
        }

        public static bool IsFertilizer(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject obj)
                && ItemCategoryRules.IsFertilizer(obj);
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(Item? item)
        {
            return TryGetPlantExtraObject(item, out SObject plantExtraObject)
                ? GetAvailableSeasonKeysCore(plantExtraObject)
                : Array.Empty<string>();
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(string? plantExtraItemId)
        {
            if (!TryCreatePlantExtraObject(plantExtraItemId ?? string.Empty, out SObject? plantExtraObject) || plantExtraObject is null)
                return Array.Empty<string>();

            return GetAvailableSeasonKeysCore(plantExtraObject);
        }

        public static bool IsSeasonalItem(Item? item)
        {
            IReadOnlyCollection<string> seasonKeys = GetAvailableSeasonKeys(item);
            return seasonKeys.Count > 0 && seasonKeys.Count < AllSeasonKeys.Count;
        }

        public static bool IsSeasonalItem(string? plantExtraItemId)
        {
            IReadOnlyCollection<string> seasonKeys = GetAvailableSeasonKeys(plantExtraItemId);
            return seasonKeys.Count > 0 && seasonKeys.Count < AllSeasonKeys.Count;
        }

        private static bool MatchesPlantExtraMembership(SObject obj)
        {
            return IsTreeFruitObject(obj)
                || IsTreeSaplingObject(obj)
                || IsFlowerObject(obj)
                || IsFlowerSeedSpecialSeedObject(obj)
                || IsMushroomObject(obj)
                || IsTappedProductObject(obj)
                || IsFertilizerObject(obj);
        }

        private static bool IsTreeFruitObject(SObject obj)
        {
            return MatchesAnyKnownName(obj, TreeFruitNames);
        }

        private static bool IsTreeSaplingObject(SObject obj)
        {
            return MatchesAnyKnownName(obj, TreeSaplingNames)
                || MatchesAnyKnownName(obj, TreeSeedNames);
        }

        private static bool IsFlowerObject(SObject obj)
        {
            return ItemCategoryRules.IsFlower(obj)
                && MatchesAnyKnownName(obj, FlowerNames);
        }

        private static bool IsFlowerSeedSpecialSeedObject(SObject obj)
        {
            return MatchesAnyKnownName(obj, FlowerSeedNames)
                || MatchesAnyKnownName(obj, SpecialSeedNames);
        }

        private static bool IsMushroomObject(SObject obj)
        {
            return MatchesAnyKnownName(obj, MushroomNames);
        }

        private static bool IsTappedProductObject(SObject obj)
        {
            return ItemCategoryRules.IsSyrup(obj)
                || MatchesAnyKnownName(obj, ExplicitTappedProductNames);
        }

        private static bool IsFertilizerObject(SObject obj)
        {
            return ItemCategoryRules.IsFertilizer(obj);
        }

        private static IReadOnlyCollection<string> GetAvailableSeasonKeysCore(SObject plantExtraObject)
        {
            foreach (string candidateName in GetKnownNames(plantExtraObject))
            {
                if (SeasonKeysByName.TryGetValue(candidateName, out IReadOnlyList<string>? seasonKeys))
                    return seasonKeys;
            }

            return AllSeasonKeys;
        }

        private static bool MatchesAnyKnownName(SObject obj, ISet<string> names)
        {
            foreach (string candidateName in GetKnownNames(obj))
            {
                if (names.Contains(candidateName))
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
