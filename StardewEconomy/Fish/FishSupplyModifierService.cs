using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked fish supply into a sell-price modifier for fish items.
    /// This mirrors the crop math pattern while staying fish-specific.
    /// </summary>
    internal static class FishSupplyModifierService
    {
        private const float OversupplyPenaltyRange = FishMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = FishMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = FishMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = FishMarketTuning.MaximumSellModifier;
        private static float? _debugSellModifierOverride;

        public static bool ApplyToLiveSellPricing { get; private set; }
        public static bool HasDebugSellModifierOverride => _debugSellModifierOverride.HasValue;
        public static float MinimumAllowedSellModifier => MinimumSellModifier;
        public static float MaximumAllowedSellModifier => MaximumSellModifier;

        public static void Initialize(bool applyToLiveSellPricing)
        {
            ApplyToLiveSellPricing = applyToLiveSellPricing;
        }

        public static bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            if (float.IsNaN(modifier) || float.IsInfinity(modifier))
            {
                error = "Modifier must be a finite number.";
                return false;
            }

            if (modifier < MinimumSellModifier || modifier > MaximumSellModifier)
            {
                error = $"Modifier {modifier:0.###} is outside the allowed range {MinimumSellModifier:0.###} to {MaximumSellModifier:0.###}.";
                return false;
            }

            _debugSellModifierOverride = modifier;
            error = string.Empty;
            return true;
        }

        public static void ClearDebugSellModifierOverride()
        {
            _debugSellModifierOverride = null;
        }

        public static bool TryGetDebugSellModifierOverride(out float modifier)
        {
            if (_debugSellModifierOverride.HasValue)
            {
                modifier = _debugSellModifierOverride.Value;
                return true;
            }

            modifier = 1f;
            return false;
        }

        public static float GetSellModifier(Item? item)
        {
            if (!FishSupplyTracker.TryGetFishInfo(item, out string fishItemId, out string displayName))
                return 1f;

            return GetSellModifier(fishItemId, displayName);
        }

        public static float GetSellModifier(string? fishItemId, string? fishDisplayName = null)
        {
            if (!FishSupplyTracker.TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = FishSupplyDataService.GetSupplyScore(normalizedFishItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(fishDisplayName)
                ? FishSupplyTracker.GetFishDisplayName(normalizedFishItemId)
                : fishDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Fish supply modifier for {displayName} ({normalizedFishItemId}): supply {supplyScore:0.##} vs neutral {FishSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string fishItemId, string? fishDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(fishDisplayName)
                ? FishSupplyTracker.GetFishDisplayName(fishItemId)
                : fishDisplayName;
            float supplyScore = FishSupplyDataService.GetSupplyScore(fishItemId);
            float modifier = GetSellModifier(fishItemId, displayName);
            return $"{displayName} ({fishItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - FishSupplyDataService.NeutralSupplyScore;

            if (deltaFromNeutral > 0f)
            {
                unclampedModifier = 1f - (deltaFromNeutral / OversupplyPenaltyRange);
            }
            else if (deltaFromNeutral < 0f)
            {
                unclampedModifier = 1f + (MathF.Abs(deltaFromNeutral) / UndersupplyBonusRange);
            }

            return Math.Clamp(unclampedModifier, MinimumSellModifier, MaximumSellModifier);
        }
    }
}
