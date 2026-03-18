using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for forageable traits and seasonal availability.
    /// </summary>
    internal static class ForageableTraitService
    {
        internal static IMonitor? Monitor;

        public static ForageableEconomicTrait GetTraits(Item? item)
        {
            if (!ForageableEconomyItemRules.TryGetForageableObject(item, out SObject forageableObject))
                return ForageableEconomicTrait.None;

            return GetTraitsForItem(forageableObject);
        }

        public static ForageableEconomicTrait GetTraits(string? forageableItemId)
        {
            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return ForageableEconomicTrait.None;

            if (!ForageableEconomyItemRules.TryCreateForageableObject(normalizedForageableItemId, out SObject? forageableObject)
                || forageableObject is null)
            {
                return ForageableEconomicTrait.None;
            }

            return GetTraitsForItem(forageableObject);
        }

        public static bool HasTrait(Item? item, ForageableEconomicTrait trait)
        {
            if (trait == ForageableEconomicTrait.None)
                return false;

            ForageableEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? forageableItemId, ForageableEconomicTrait trait)
        {
            if (trait == ForageableEconomicTrait.None)
                return false;

            ForageableEconomicTrait traits = GetTraits(forageableItemId);
            return (traits & trait) == trait;
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(Item? item)
        {
            if (!ForageableEconomyItemRules.TryGetForageableObject(item, out SObject forageableObject))
                return Array.Empty<string>();

            return ForageableEconomyItemRules.GetAvailableSeasonKeys(forageableObject.ItemId);
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(string? forageableItemId)
        {
            return ForageableEconomyItemRules.GetAvailableSeasonKeys(forageableItemId);
        }

        public static bool IsAvailableInSeason(Item? item, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(item)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAvailableInSeason(string? forageableItemId, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(forageableItemId)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static string FormatTraits(ForageableEconomicTrait traits)
        {
            if (traits == ForageableEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (ForageableEconomicTrait trait in Enum.GetValues<ForageableEconomicTrait>())
            {
                if (trait == ForageableEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Forageable traits: <null item> -> None";

            ForageableEconomicTrait traits = GetTraits(item);
            string seasons = string.Join(", ", GetAvailableSeasonKeys(item));
            if (string.IsNullOrWhiteSpace(seasons))
                seasons = "none";

            return $"Forageable traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)} [seasons: {seasons}]";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static ForageableEconomicTrait GetTraitsForItem(SObject forageableObject)
        {
            ForageableEconomicTrait traits = ForageableEconomicTrait.None;

            if (ForageableEconomyItemRules.IsSeasonalForage(forageableObject))
                traits |= ForageableEconomicTrait.SeasonalForage;

            if (ForageableEconomyItemRules.IsBeachForage(forageableObject))
                traits |= ForageableEconomicTrait.BeachForage;

            if (ForageableEconomyItemRules.IsForestForage(forageableObject))
                traits |= ForageableEconomicTrait.ForestForage;

            if (ForageableEconomyItemRules.IsDesertForage(forageableObject))
                traits |= ForageableEconomicTrait.DesertForage;

            if (ForageableEconomyItemRules.IsGingerIslandForage(forageableObject))
                traits |= ForageableEconomicTrait.GingerIslandForage;

            if (ForageableEconomyItemRules.IsGatheredFlowersWildEdibles(forageableObject))
                traits |= ForageableEconomicTrait.GatheredFlowersWildEdibles;

            return traits;
        }
    }
}
