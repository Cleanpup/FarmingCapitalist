using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping plant-extra traits and seasonal availability.
    /// </summary>
    internal static class PlantExtraTraitService
    {
        internal static IMonitor? Monitor;

        public static PlantExtraEconomicTrait GetTraits(Item? item)
        {
            if (!PlantExtraEconomyItemRules.TryGetPlantExtraObject(item, out SObject plantExtraObject))
                return PlantExtraEconomicTrait.None;

            return GetTraitsForItem(plantExtraObject);
        }

        public static PlantExtraEconomicTrait GetTraits(string? plantExtraItemId)
        {
            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return PlantExtraEconomicTrait.None;

            if (!PlantExtraEconomyItemRules.TryCreatePlantExtraObject(normalizedPlantExtraItemId, out SObject? plantExtraObject)
                || plantExtraObject is null)
            {
                return PlantExtraEconomicTrait.None;
            }

            return GetTraitsForItem(plantExtraObject);
        }

        public static bool HasTrait(Item? item, PlantExtraEconomicTrait trait)
        {
            if (trait == PlantExtraEconomicTrait.None)
                return false;

            PlantExtraEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? plantExtraItemId, PlantExtraEconomicTrait trait)
        {
            if (trait == PlantExtraEconomicTrait.None)
                return false;

            PlantExtraEconomicTrait traits = GetTraits(plantExtraItemId);
            return (traits & trait) == trait;
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(Item? item)
        {
            return PlantExtraEconomyItemRules.GetAvailableSeasonKeys(item);
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(string? plantExtraItemId)
        {
            return PlantExtraEconomyItemRules.GetAvailableSeasonKeys(plantExtraItemId);
        }

        public static bool IsAvailableInSeason(Item? item, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(item)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAvailableInSeason(string? plantExtraItemId, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(plantExtraItemId)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static string FormatTraits(PlantExtraEconomicTrait traits)
        {
            if (traits == PlantExtraEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (PlantExtraEconomicTrait trait in Enum.GetValues<PlantExtraEconomicTrait>())
            {
                if (trait == PlantExtraEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "PlantExtra traits: <null item> -> None";

            PlantExtraEconomicTrait traits = GetTraits(item);
            string seasons = string.Join(", ", GetAvailableSeasonKeys(item));
            if (string.IsNullOrWhiteSpace(seasons))
                seasons = "none";

            return $"PlantExtra traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)} [seasons: {seasons}]";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static PlantExtraEconomicTrait GetTraitsForItem(SObject plantExtraObject)
        {
            PlantExtraEconomicTrait traits = PlantExtraEconomicTrait.None;

            if (PlantExtraEconomyItemRules.IsTreeFruit(plantExtraObject))
                traits |= PlantExtraEconomicTrait.TreeFruit;

            if (PlantExtraEconomyItemRules.IsTreeSapling(plantExtraObject))
                traits |= PlantExtraEconomicTrait.TreeSapling;

            if (PlantExtraEconomyItemRules.IsFlower(plantExtraObject))
                traits |= PlantExtraEconomicTrait.Flower;

            if (PlantExtraEconomyItemRules.IsFlowerSeedSpecialSeed(plantExtraObject))
                traits |= PlantExtraEconomicTrait.FlowerSeedSpecialSeed;

            if (PlantExtraEconomyItemRules.IsMushroom(plantExtraObject))
                traits |= PlantExtraEconomicTrait.Mushroom;

            if (PlantExtraEconomyItemRules.IsTappedProduct(plantExtraObject))
                traits |= PlantExtraEconomicTrait.TappedProduct;

            if (PlantExtraEconomyItemRules.IsFertilizer(plantExtraObject))
                traits |= PlantExtraEconomicTrait.Fertilizer;

            return traits;
        }
    }
}
