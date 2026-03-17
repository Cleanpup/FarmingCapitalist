using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    internal enum FishEconomyClassification
    {
        None,
        RawFish,
        SmokedFish,
        Roe,
        AgedRoe,
        SeaweedAlgae
    }

    /// <summary>
    /// Fish-owned classification and config gating. Base category predicates remain in ItemCategoryRules.
    /// </summary>
    internal static class FishEconomyItemRules
    {
        private static readonly HashSet<string> SeaweedAndAlgaeIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "152",
            "153",
            "157"
        };
        private static readonly HashSet<string> FishingJellyIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "SeaJelly",
            "RiverJelly",
            "CaveJelly"
        };

        private static FishEconomyClassificationConfig _fishClassificationConfig = new();

        public static void Initialize(FishEconomyClassificationConfig? fishClassificationConfig)
        {
            _fishClassificationConfig = fishClassificationConfig ?? new FishEconomyClassificationConfig();
        }

        public static bool IsFishEconomyEligible(Item? item)
        {
            FishEconomyClassification classification = GetFishEconomyClassification(item);
            return classification != FishEconomyClassification.None
                && IsFishEconomyClassificationEnabled(classification);
        }

        public static bool TryGetFishEconomyClassification(
            Item? item,
            out FishEconomyClassification classification,
            out bool isEligible,
            bool logDecision = false,
            string? context = null
        )
        {
            classification = GetFishEconomyClassification(item);
            isEligible = classification != FishEconomyClassification.None
                && IsFishEconomyClassificationEnabled(classification);

            if (classification == FishEconomyClassification.None)
                return false;

            if (logDecision)
                LogFishEconomyClassification(item, classification, isEligible, context);

            return true;
        }

        public static FishEconomyClassification GetFishEconomyClassification(Item? item)
        {
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return FishEconomyClassification.None;

            if (TryGetFishPreserveClassification(obj, out FishEconomyClassification preserveClassification))
                return preserveClassification;

            if (IsSeaweedOrAlgae(obj))
                return FishEconomyClassification.SeaweedAlgae;

            // Vanilla fishing jellies count as fish catches, but they are not assigned FishCategory.
            if (IsFishingJelly(obj))
                return FishEconomyClassification.RawFish;

            return ItemCategoryRules.IsFish(obj)
                ? FishEconomyClassification.RawFish
                : FishEconomyClassification.None;
        }

        public static bool TryGetFishPreserveSourceItemId(Item? item, out string sourceItemId)
        {
            sourceItemId = string.Empty;
            return item is SObject obj
                && TryGetFishPreserveSourceItemId(obj, out sourceItemId);
        }

        public static bool TryGetFishPreserveSourceItemId(SObject obj, out string sourceItemId)
        {
            sourceItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(obj.preservedParentSheetIndex.Value))
                return false;

            return TryNormalizeItemId(obj.preservedParentSheetIndex.Value, out sourceItemId);
        }

        public static string GetFishEconomyClassificationLabel(FishEconomyClassification classification)
        {
            return classification switch
            {
                FishEconomyClassification.RawFish => "RawFish",
                FishEconomyClassification.SmokedFish => "SmokedFish",
                FishEconomyClassification.Roe => "Roe",
                FishEconomyClassification.AgedRoe => "AgedRoe",
                FishEconomyClassification.SeaweedAlgae => "SeaweedAlgae",
                _ => "None"
            };
        }

        private static bool IsFishEconomyClassificationEnabled(FishEconomyClassification classification)
        {
            return classification switch
            {
                FishEconomyClassification.RawFish => _fishClassificationConfig.IncludeRawFish,
                FishEconomyClassification.SmokedFish => _fishClassificationConfig.IncludeSmokedFish,
                FishEconomyClassification.Roe => _fishClassificationConfig.IncludeRoe,
                FishEconomyClassification.AgedRoe => _fishClassificationConfig.IncludeAgedRoe,
                FishEconomyClassification.SeaweedAlgae => _fishClassificationConfig.IncludeSeaweedAlgae,
                _ => false
            };
        }

        private static bool TryGetFishPreserveClassification(SObject obj, out FishEconomyClassification classification)
        {
            classification = FishEconomyClassification.None;
            if (!obj.preserve.Value.HasValue)
                return false;

            classification = obj.preserve.Value.Value switch
            {
                SObject.PreserveType.SmokedFish => FishEconomyClassification.SmokedFish,
                SObject.PreserveType.Roe => FishEconomyClassification.Roe,
                SObject.PreserveType.AgedRoe => FishEconomyClassification.AgedRoe,
                _ => FishEconomyClassification.None
            };

            return classification != FishEconomyClassification.None;
        }

        private static bool IsSeaweedOrAlgae(SObject obj)
        {
            return TryNormalizeItemId(obj.ItemId, out string normalizedItemId)
                && SeaweedAndAlgaeIds.Contains(normalizedItemId);
        }

        private static bool IsFishingJelly(SObject obj)
        {
            return TryNormalizeItemId(obj.ItemId, out string normalizedItemId)
                && FishingJellyIds.Contains(normalizedItemId);
        }

        private static bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            normalizedItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawItemId))
                return false;

            string candidate = rawItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedItemId = candidate;
            return true;
        }

        private static void LogFishEconomyClassification(
            Item? item,
            FishEconomyClassification classification,
            bool isEligible,
            string? context
        )
        {
            string displayName = item?.DisplayName ?? item?.Name ?? "<unknown>";
            string qualifiedItemId = item?.QualifiedItemId ?? "<none>";
            string reason = string.IsNullOrWhiteSpace(context)
                ? "fish-economy"
                : context.Trim();

            VerbosePriceTraceLogger.Log(
                $"{reason}: {displayName} ({qualifiedItemId}) classified as {GetFishEconomyClassificationLabel(classification)} -> {(isEligible ? "included" : "excluded")}"
            );
        }
    }
}
