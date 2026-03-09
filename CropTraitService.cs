using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for mod-defined crop economic traits.
    /// This is a shared classification layer for future balancing/market systems.
    /// </summary>
    internal static class CropTraitService
    {
        internal static IMonitor? Monitor;

        private const int FastCropMaxDays = 5;
        private const int MediumCropMaxDays = 9;

        // Seed bucket thresholds are based on default player-facing seed prices:
        // Cheap: up to 40g, Mid: 50-199g, Expensive: 200g+.
        private const int CheapSeedPriceMaxExclusive = 50;
        private const int ExpensiveSeedPriceMinInclusive = 200;

        private static readonly StringComparer ItemIdComparer = StringComparer.OrdinalIgnoreCase;
        private static Dictionary<string, string> _harvestToSeedLookup = new(ItemIdComparer);
        private static IDictionary<string, CropData>? _cachedCropDataReference;
        private static int _cachedCropDataCount = -1;

        public static CropEconomicTrait GetTraits(Item? item)
        {
            if (!TryResolveSeedItemId(item, out string seedItemId))
                return CropEconomicTrait.None;

            CropEconomicTrait traits = GetTraitsForSeed(seedItemId, sourceItem: item);
            return traits;
        }

        public static CropEconomicTrait GetTraits(string? seedItemId)
        {
            if (!TryNormalizeSeedItemId(seedItemId, out string normalizedSeedItemId))
                return CropEconomicTrait.None;

            return GetTraitsForSeed(normalizedSeedItemId, sourceItem: null);
        }

        public static bool HasTrait(Item? item, CropEconomicTrait trait)
        {
            if (trait == CropEconomicTrait.None)
                return false;

            CropEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool TryGetCropData(Item? item, out string seedItemId, out CropData? cropData)
        {
            seedItemId = string.Empty;
            cropData = null;

            if (!TryResolveSeedItemId(item, out seedItemId))
                return false;

            return Crop.TryGetData(seedItemId, out cropData);
        }

        public static bool IsCropSeed(Item? item)
        {
            if (item is null || !ItemCategoryRules.IsSeed(item))
                return false;

            return TryNormalizeSeedItemId(item.ItemId, out _);
        }

        public static bool IsRegrowthCrop(Item? item) => HasTrait(item, CropEconomicTrait.Regrowth);

        public static bool IsSingleHarvestCrop(Item? item) => HasTrait(item, CropEconomicTrait.SingleHarvest);

        public static bool IsMultiYieldCrop(Item? item) => HasTrait(item, CropEconomicTrait.MultiYield);

        public static bool IsSingleYieldCrop(Item? item) => HasTrait(item, CropEconomicTrait.SingleYield);

        public static bool IsFastCrop(Item? item) => HasTrait(item, CropEconomicTrait.FastCrop);

        public static bool IsMediumCrop(Item? item) => HasTrait(item, CropEconomicTrait.MediumCrop);

        public static bool IsSlowCrop(Item? item) => HasTrait(item, CropEconomicTrait.SlowCrop);

        public static bool IsCheapSeedCrop(Item? item) => HasTrait(item, CropEconomicTrait.CheapSeed);

        public static bool IsMidSeedCrop(Item? item) => HasTrait(item, CropEconomicTrait.MidSeed);

        public static bool IsExpensiveSeedCrop(Item? item) => HasTrait(item, CropEconomicTrait.ExpensiveSeed);

        public static string FormatTraits(CropEconomicTrait traits)
        {
            if (traits == CropEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (CropEconomicTrait trait in Enum.GetValues<CropEconomicTrait>())
            {
                if (trait == CropEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Crop traits: <null item> -> None";

            CropEconomicTrait traits = GetTraits(item);
            return $"Crop traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static CropEconomicTrait GetTraitsForSeed(string seedItemId, Item? sourceItem)
        {
            if (!Crop.TryGetData(seedItemId, out CropData? cropData) || cropData is null)
                return CropEconomicTrait.None;

            CropEconomicTrait traits = CropEconomicTrait.None;

            traits |= GetHarvestTypeTrait(cropData);
            traits |= GetYieldTypeTrait(cropData);
            traits |= GetGrowthSpeedTrait(cropData);

            if (TryGetSeedPurchasePrice(seedItemId, sourceItem, out int seedPurchasePrice))
                traits |= GetSeedCostTrait(seedPurchasePrice);

            return traits;
        }

        private static CropEconomicTrait GetHarvestTypeTrait(CropData cropData)
        {
            return cropData.RegrowDays > 0
                ? CropEconomicTrait.Regrowth
                : CropEconomicTrait.SingleHarvest;
        }

        private static CropEconomicTrait GetYieldTypeTrait(CropData cropData)
        {
            bool isMultiYield =
                cropData.HarvestMinStack > 1
                || cropData.HarvestMaxStack > 1
                || cropData.HarvestMaxIncreasePerFarmingLevel > 0f
                || cropData.ExtraHarvestChance > 0.0;

            return isMultiYield
                ? CropEconomicTrait.MultiYield
                : CropEconomicTrait.SingleYield;
        }

        private static CropEconomicTrait GetGrowthSpeedTrait(CropData cropData)
        {
            int totalDaysToFirstHarvest = GetDaysToFirstHarvest(cropData);

            if (totalDaysToFirstHarvest <= FastCropMaxDays)
                return CropEconomicTrait.FastCrop;

            if (totalDaysToFirstHarvest <= MediumCropMaxDays)
                return CropEconomicTrait.MediumCrop;

            return CropEconomicTrait.SlowCrop;
        }

        private static int GetDaysToFirstHarvest(CropData cropData)
        {
            if (cropData.DaysInPhase is null || cropData.DaysInPhase.Count == 0)
                return 0;

            int totalDays = 0;
            foreach (int phaseDays in cropData.DaysInPhase)
            {
                if (phaseDays > 0)
                    totalDays += phaseDays;
            }

            return totalDays;
        }

        private static CropEconomicTrait GetSeedCostTrait(int seedPrice)
        {
            if (seedPrice >= ExpensiveSeedPriceMinInclusive)
                return CropEconomicTrait.ExpensiveSeed;

            if (seedPrice < CheapSeedPriceMaxExclusive)
                return CropEconomicTrait.CheapSeed;

            return CropEconomicTrait.MidSeed;
        }

        private static bool TryGetSeedPurchasePrice(string seedItemId, Item? sourceItem, out int seedPrice)
        {
            seedPrice = 0;

            if (Game1.objectData.TryGetValue(seedItemId, out var seedObjectData))
            {
                // For seeds, Data/Objects price reflects the canonical seed-cost baseline.
                seedPrice = Math.Max(0, seedObjectData.Price);
                if (seedPrice > 0)
                    return true;
            }

            if (sourceItem is SObject sourceSeedObject
                && string.Equals(sourceSeedObject.ItemId, seedItemId, StringComparison.OrdinalIgnoreCase))
            {
                seedPrice = Math.Max(0, sourceSeedObject.salePrice(ignoreProfitMargins: true));
                if (seedPrice > 0)
                    return true;
            }

            SObject? tempSeedObject = ItemRegistry.Create<SObject>("(O)" + seedItemId, allowNull: true);
            if (tempSeedObject is not null)
            {
                seedPrice = Math.Max(0, tempSeedObject.salePrice(ignoreProfitMargins: true));
                if (seedPrice > 0)
                    return true;
            }

            return false;
        }

        private static bool TryResolveSeedItemId(Item? item, out string seedItemId)
        {
            seedItemId = string.Empty;

            if (item is null)
                return false;

            if (TryNormalizeSeedItemId(item.ItemId, out string directSeedId))
            {
                seedItemId = directSeedId;
                return true;
            }

            if (item is SObject obj && TryResolveSeedFromHarvestItem(obj.ItemId, out string harvestSeedId))
            {
                seedItemId = harvestSeedId;
                return true;
            }

            return false;
        }

        private static bool TryNormalizeSeedItemId(string? itemId, out string normalizedSeedItemId)
        {
            normalizedSeedItemId = string.Empty;

            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            string candidate = itemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (!Crop.TryGetData(candidate, out _))
                return false;

            normalizedSeedItemId = candidate;
            return true;
        }

        private static bool TryResolveSeedFromHarvestItem(string? harvestItemId, out string seedItemId)
        {
            seedItemId = string.Empty;

            if (string.IsNullOrWhiteSpace(harvestItemId))
                return false;

            EnsureHarvestLookup();
            if (_harvestToSeedLookup.TryGetValue(harvestItemId, out string? resolvedSeedItemId)
                && !string.IsNullOrWhiteSpace(resolvedSeedItemId))
            {
                seedItemId = resolvedSeedItemId;
                return true;
            }

            return false;
        }

        private static void EnsureHarvestLookup()
        {
            IDictionary<string, CropData> cropData = Game1.cropData;
            if (cropData is null || cropData.Count == 0)
            {
                _harvestToSeedLookup = new Dictionary<string, string>(ItemIdComparer);
                _cachedCropDataReference = cropData;
                _cachedCropDataCount = 0;
                return;
            }

            if (ReferenceEquals(cropData, _cachedCropDataReference)
                && _cachedCropDataCount == cropData.Count
                && _harvestToSeedLookup.Count > 0)
            {
                return;
            }

            Dictionary<string, string> lookup = new(ItemIdComparer);
            foreach (KeyValuePair<string, CropData> pair in cropData)
            {
                string seedId = pair.Key;
                CropData data = pair.Value;

                if (string.IsNullOrWhiteSpace(seedId) || data is null || string.IsNullOrWhiteSpace(data.HarvestItemId))
                    continue;

                if (!lookup.ContainsKey(data.HarvestItemId))
                    lookup[data.HarvestItemId] = seedId;
            }

            _harvestToSeedLookup = lookup;
            _cachedCropDataReference = cropData;
            _cachedCropDataCount = cropData.Count;
        }
    }
}
