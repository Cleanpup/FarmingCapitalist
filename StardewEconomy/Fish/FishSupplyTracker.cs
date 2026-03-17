using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Shared fish tracking helpers used by sale hooks and future fish pricing/debug flows.
    /// </summary>
    internal static class FishSupplyTracker
    {
        private const string SmokedFishVariantPrefix = "SmokedFish/";
        private const string RoeVariantPrefix = "812/";
        private const string AgedRoeVariantPrefix = "447/";
        private const string SmokedFishQualifiedItemId = "(O)SmokedFish";
        private const string RoeQualifiedItemId = "(O)812";
        private const string AgedRoeQualifiedItemId = "(O)447";

        public static void TrackSale(Item? item, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (FishSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryGetFishInfo(item, out string fishItemId, out string displayName, logDecision: true, decisionContext: "fish-track"))
                return;

            TrackFishSale(fishItemId, displayName, quantity, source);
        }

        public static void TrackFishSale(string fishItemId, string displayName, int quantity, string source)
        {
            if (quantity <= 0)
                return;

            if (FishSupplyModifierService.HasDebugSellModifierOverride)
                return;

            if (!TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return;

            if (!TryCreateFishEconomyObject(normalizedFishItemId, out _))
                return;

            FishSupplyDataService.AddSupply(normalizedFishItemId, quantity, displayName, source);
        }

        public static void TrackItems(IEnumerable<Item> items, string source)
        {
            if (FishSupplyModifierService.HasDebugSellModifierOverride)
                return;

            Dictionary<string, (string DisplayName, int Quantity)> totalsByFish = new(StringComparer.OrdinalIgnoreCase);
            foreach (Item item in items)
            {
                if (!TryGetFishInfo(item, out string fishItemId, out string displayName, logDecision: true, decisionContext: "fish-track"))
                    continue;

                totalsByFish.TryGetValue(fishItemId, out (string DisplayName, int Quantity) existing);
                totalsByFish[fishItemId] = (displayName, existing.Quantity + item.Stack);
            }

            foreach (KeyValuePair<string, (string DisplayName, int Quantity)> pair in totalsByFish)
            {
                TrackFishSale(pair.Key, pair.Value.DisplayName, pair.Value.Quantity, source);
            }
        }

        public static bool TryGetFishInfo(Item? item, out string fishItemId, out string displayName)
        {
            return TryGetFishInfo(item, out fishItemId, out displayName, logDecision: false, decisionContext: null);
        }

        private static bool TryGetFishInfo(
            Item? item,
            out string fishItemId,
            out string displayName,
            bool logDecision,
            string? decisionContext
        )
        {
            fishItemId = string.Empty;
            displayName = string.Empty;

            if (item is not SObject obj)
                return false;

            if (!FishEconomyItemRules.TryGetFishEconomyClassification(
                    obj,
                    out FishEconomyClassification classification,
                    out bool isEligible,
                    logDecision,
                    decisionContext
                ))
                return false;

            if (!isEligible)
                return false;

            if (!TryGetFishEconomyItemId(obj, classification, out fishItemId))
                return false;

            displayName = string.IsNullOrWhiteSpace(obj.DisplayName)
                ? obj.Name
                : obj.DisplayName;

            return true;
        }

        public static bool TryResolveFishItemId(string? rawInput, out string fishItemId, out string displayName)
        {
            fishItemId = string.Empty;
            displayName = string.Empty;

            if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(rawInput))
                return false;

            string normalizedInput = rawInput.Trim();
            if (TryCreateFishEconomyObject(normalizedInput, out SObject? directObject))
                return TryGetFishInfo(directObject, out fishItemId, out displayName);

            foreach (KeyValuePair<string, ObjectData> pair in Game1.objectData)
            {
                if (!MatchesInput(pair.Value, normalizedInput))
                    continue;

                if (!TryCreateFishEconomyObject(pair.Key, out SObject? namedObject))
                    continue;

                return TryGetFishInfo(namedObject, out fishItemId, out displayName);
            }

            return false;
        }

        public static bool TryNormalizeFishItemId(string? rawFishItemId, out string normalizedFishItemId)
        {
            normalizedFishItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawFishItemId))
                return false;

            string candidate = rawFishItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedFishItemId = candidate;
            return true;
        }

        public static string GetFishDisplayName(string? fishItemId)
        {
            if (!TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return fishItemId?.Trim() ?? string.Empty;

            if (TryCreateFishEconomyObject(normalizedFishItemId, out SObject? fishObject) && fishObject is not null)
            {
                string objectDisplayName = string.IsNullOrWhiteSpace(fishObject.DisplayName)
                    ? fishObject.Name
                    : fishObject.DisplayName;
                if (!string.IsNullOrWhiteSpace(objectDisplayName))
                    return objectDisplayName;
            }

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedFishItemId, out ObjectData? data))
            {
                if (!string.IsNullOrWhiteSpace(data.DisplayName))
                    return data.DisplayName;

                if (!string.IsNullOrWhiteSpace(data.Name))
                    return data.Name;
            }

            return normalizedFishItemId;
        }

        internal static bool TryGetFishMarketInfo(
            string? fishItemId,
            out string displayName,
            out FishEconomyClassification classification,
            out string sourceFishItemId
        )
        {
            displayName = string.Empty;
            classification = FishEconomyClassification.None;
            sourceFishItemId = string.Empty;

            if (!TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return false;

            if (!TryCreateFishEconomyObject(normalizedFishItemId, out SObject? fishObject) || fishObject is null)
                return false;

            classification = FishEconomyItemRules.GetFishEconomyClassification(fishObject);
            if (classification == FishEconomyClassification.None)
                return false;

            displayName = string.IsNullOrWhiteSpace(fishObject.DisplayName)
                ? fishObject.Name
                : fishObject.DisplayName;

            if (!FishEconomyItemRules.TryGetFishPreserveSourceItemId(fishObject, out sourceFishItemId))
                TryNormalizeFishItemId(fishObject.ItemId, out sourceFishItemId);

            return !string.IsNullOrWhiteSpace(sourceFishItemId);
        }

        internal static bool TryGetSourceFishItemId(string? fishItemId, out string sourceFishItemId)
        {
            sourceFishItemId = string.Empty;
            return TryGetFishMarketInfo(fishItemId, out _, out _, out sourceFishItemId);
        }

        private static bool TryGetFishEconomyItemId(SObject obj, FishEconomyClassification classification, out string fishItemId)
        {
            fishItemId = string.Empty;
            if (classification is FishEconomyClassification.RawFish or FishEconomyClassification.SeaweedAlgae)
                return TryNormalizeFishItemId(obj.ItemId, out fishItemId);

            if (!FishEconomyItemRules.TryGetFishPreserveSourceItemId(obj, out string sourceItemId))
                return TryNormalizeFishItemId(obj.ItemId, out fishItemId);

            fishItemId = classification switch
            {
                FishEconomyClassification.SmokedFish => SmokedFishVariantPrefix + sourceItemId,
                FishEconomyClassification.Roe => RoeVariantPrefix + sourceItemId,
                FishEconomyClassification.AgedRoe => AgedRoeVariantPrefix + sourceItemId,
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(fishItemId);
        }

        private static bool TryCreateFishEconomyObject(string rawItemId, out SObject? obj)
        {
            obj = null;

            if (!TryNormalizeFishItemId(rawItemId, out string normalizedFishItemId))
                return false;

            if (TryParsePreservedFishItemId(
                    normalizedFishItemId,
                    out string outputQualifiedItemId,
                    out SObject.PreserveType preserveType,
                    out string sourceItemId
                ))
            {
                obj = ItemRegistry.Create<SObject>(outputQualifiedItemId, allowNull: true);
                if (obj is null)
                    return false;

                obj.preserve.Value = preserveType;
                obj.preservedParentSheetIndex.Value = sourceItemId;
                return FishEconomyItemRules.GetFishEconomyClassification(obj) != FishEconomyClassification.None;
            }

            obj = ItemRegistry.Create<SObject>("(O)" + normalizedFishItemId, allowNull: true);
            return obj is not null
                && FishEconomyItemRules.GetFishEconomyClassification(obj) != FishEconomyClassification.None;
        }

        private static bool TryParsePreservedFishItemId(
            string normalizedFishItemId,
            out string outputQualifiedItemId,
            out SObject.PreserveType preserveType,
            out string sourceItemId
        )
        {
            outputQualifiedItemId = string.Empty;
            preserveType = default;
            sourceItemId = string.Empty;

            if (TryMatchPreservedFishItemId(
                    normalizedFishItemId,
                    SmokedFishVariantPrefix,
                    SmokedFishQualifiedItemId,
                    SObject.PreserveType.SmokedFish,
                    out sourceItemId
                ))
            {
                outputQualifiedItemId = SmokedFishQualifiedItemId;
                preserveType = SObject.PreserveType.SmokedFish;
                return true;
            }

            if (TryMatchPreservedFishItemId(
                    normalizedFishItemId,
                    RoeVariantPrefix,
                    RoeQualifiedItemId,
                    SObject.PreserveType.Roe,
                    out sourceItemId
                ))
            {
                outputQualifiedItemId = RoeQualifiedItemId;
                preserveType = SObject.PreserveType.Roe;
                return true;
            }

            if (TryMatchPreservedFishItemId(
                    normalizedFishItemId,
                    AgedRoeVariantPrefix,
                    AgedRoeQualifiedItemId,
                    SObject.PreserveType.AgedRoe,
                    out sourceItemId
                ))
            {
                outputQualifiedItemId = AgedRoeQualifiedItemId;
                preserveType = SObject.PreserveType.AgedRoe;
                return true;
            }

            return false;
        }

        private static bool TryMatchPreservedFishItemId(
            string normalizedFishItemId,
            string prefix,
            string outputQualifiedItemId,
            SObject.PreserveType preserveType,
            out string sourceItemId
        )
        {
            _ = outputQualifiedItemId;
            _ = preserveType;
            sourceItemId = string.Empty;
            if (!normalizedFishItemId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string rawSourceItemId = normalizedFishItemId.Substring(prefix.Length);
            return TryNormalizeFishItemId(rawSourceItemId, out sourceItemId);
        }

        private static bool MatchesInput(ObjectData data, string input)
        {
            return string.Equals(data.DisplayName, input, StringComparison.OrdinalIgnoreCase)
                || string.Equals(data.Name, input, StringComparison.OrdinalIgnoreCase);
        }
    }
}
