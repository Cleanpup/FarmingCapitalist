using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Forageable-owned eligibility and normalization helpers.
    /// Membership intentionally stays explicit to cover forage edge cases without broadening into crops or tree fruit.
    /// </summary>
    internal static class ForageableEconomyItemRules
    {
        private static readonly HashSet<string> IncludedForageableItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "16",  // Wild Horseradish
            "18",  // Daffodil
            "20",  // Leek
            "22",  // Dandelion
            "88",  // Coconut
            "90",  // Cactus Fruit
            "257", // Morel
            "259", // Fiddlehead Fern
            "281", // Chanterelle
            "283", // Holly
            "296", // Salmonberry
            "372", // Clam
            "392", // Nautilus Shell
            "393", // Coral
            "394", // Rainbow Shell
            "396", // Spice Berry
            "397", // Sea Urchin
            "398", // Grape
            "399", // Spring Onion
            "402", // Sweet Pea
            "404", // Common Mushroom
            "406", // Wild Plum
            "408", // Hazelnut
            "410", // Blackberry
            "412", // Winter Root
            "414", // Crystal Fruit
            "416", // Snow Yam
            "418", // Crocus
            "420", // Red Mushroom
            "422", // Purple Mushroom
            "718", // Cockle
            "719", // Mussel
            "723", // Oyster
            "829", // Ginger
            "851"  // Magma Cap
        };

        private static readonly HashSet<string> BeachForageItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "372",
            "392",
            "393",
            "394",
            "397",
            "718",
            "719",
            "723"
        };

        private static readonly HashSet<string> ForestForageItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "16",
            "257",
            "259",
            "281",
            "296",
            "399",
            "404",
            "406",
            "408",
            "410",
            "420",
            "422"
        };

        private static readonly HashSet<string> DesertForageItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "88",
            "90"
        };

        private static readonly HashSet<string> GingerIslandForageItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "88",
            "829",
            "851"
        };

        private static readonly HashSet<string> GatheredFlowersWildEdiblesItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "16",
            "18",
            "20",
            "22",
            "283",
            "296",
            "396",
            "398",
            "399",
            "402",
            "406",
            "408",
            "410",
            "412",
            "414",
            "416",
            "418"
        };

        private static readonly IReadOnlyList<string> AllSeasonKeys =
            new[] { "spring", "summer", "fall", "winter" };

        private static readonly Dictionary<string, IReadOnlyList<string>> SeasonOverridesByItemId =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["88"] = AllSeasonKeys,                // Coconut
                ["90"] = AllSeasonKeys,                // Cactus Fruit
                ["296"] = new[] { "spring" },         // Salmonberry
                ["372"] = AllSeasonKeys,               // Clam
                ["393"] = AllSeasonKeys,               // Coral
                ["397"] = AllSeasonKeys,               // Sea Urchin
                ["416"] = new[] { "winter" },         // Snow Yam
                ["422"] = AllSeasonKeys,               // Purple Mushroom
                ["718"] = AllSeasonKeys,               // Cockle
                ["719"] = AllSeasonKeys,               // Mussel
                ["723"] = AllSeasonKeys,               // Oyster
                ["829"] = AllSeasonKeys,               // Ginger
                ["851"] = AllSeasonKeys                // Magma Cap
            };

        public static bool IsForageableEligible(Item? item)
        {
            return TryGetForageableObject(item, out _);
        }

        public static bool TryGetForageableObject(Item? item, out SObject forageableObject)
        {
            forageableObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!TryNormalizeForageableItemId(obj.ItemId, out string normalizedItemId))
                return false;

            if (!IsForageableItemId(normalizedItemId))
                return false;

            forageableObject = obj;
            return true;
        }

        public static bool TryNormalizeForageableItemId(string? rawForageableItemId, out string normalizedForageableItemId)
        {
            normalizedForageableItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawForageableItemId))
                return false;

            string candidate = rawForageableItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedForageableItemId = candidate;
            return true;
        }

        public static bool TryCreateForageableObject(string rawItemId, out SObject? forageableObject)
        {
            forageableObject = null;
            if (!TryNormalizeForageableItemId(rawItemId, out string normalizedForageableItemId))
                return false;

            forageableObject = ItemRegistry.Create<SObject>("(O)" + normalizedForageableItemId, allowNull: true);
            return forageableObject is not null
                && IsForageableItemId(normalizedForageableItemId);
        }

        public static bool IsForageableItemId(string? forageableItemId)
        {
            return TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId)
                && IncludedForageableItemIds.Contains(normalizedForageableItemId);
        }

        public static bool IsBeachForage(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && BeachForageItemIds.Contains(itemId);
        }

        public static bool IsForestForage(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && ForestForageItemIds.Contains(itemId);
        }

        public static bool IsDesertForage(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && DesertForageItemIds.Contains(itemId);
        }

        public static bool IsGingerIslandForage(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && GingerIslandForageItemIds.Contains(itemId);
        }

        public static bool IsGatheredFlowersWildEdibles(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && GatheredFlowersWildEdiblesItemIds.Contains(itemId);
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(string? forageableItemId)
        {
            if (!TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return Array.Empty<string>();

            if (SeasonOverridesByItemId.TryGetValue(normalizedForageableItemId, out IReadOnlyList<string>? overrideSeasons))
                return overrideSeasons;

            if (Context.IsWorldReady
                && Game1.objectData.TryGetValue(normalizedForageableItemId, out var data)
                && data is not null
                && data.ContextTags is not null)
            {
                List<string> seasonKeys = data.ContextTags
                    .Where(tag => tag.StartsWith("season_", StringComparison.OrdinalIgnoreCase))
                    .Select(tag => tag.Substring("season_".Length).Trim().ToLowerInvariant())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (seasonKeys.Any(tag => string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase)))
                    return AllSeasonKeys;

                if (seasonKeys.Count > 0)
                    return seasonKeys;
            }

            return AllSeasonKeys;
        }

        public static bool IsSeasonalForage(Item? item)
        {
            return TryGetNormalizedForageableItemId(item, out string itemId)
                && IsSeasonalForage(itemId);
        }

        public static bool IsSeasonalForage(string? forageableItemId)
        {
            IReadOnlyCollection<string> seasonKeys = GetAvailableSeasonKeys(forageableItemId);
            return seasonKeys.Count > 0 && seasonKeys.Count < AllSeasonKeys.Count;
        }

        private static bool TryGetNormalizedForageableItemId(Item? item, out string forageableItemId)
        {
            forageableItemId = string.Empty;
            return item is SObject obj
                && TryNormalizeForageableItemId(obj.ItemId, out forageableItemId)
                && IsForageableItemId(forageableItemId);
        }
    }
}
