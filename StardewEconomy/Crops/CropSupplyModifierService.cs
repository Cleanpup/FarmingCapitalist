using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for crop produce.
    /// This stays separate from persistence and can be plugged into pricing later.
    /// </summary>
    internal static class CropSupplyModifierService
    {
        private const float OversupplyPenaltyRange = CropMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = CropMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = CropMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = CropMarketTuning.MaximumSellModifier;
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
            if (!CropSupplyTracker.TryGetCropProduceInfo(item, out string produceItemId, out string displayName))
                return 1f;

            return GetSellModifier(produceItemId, displayName);
        }

        public static float GetSellModifier(string? cropProduceItemId, string? cropDisplayName = null)
        {
            if (!CropSupplyTracker.TryNormalizeCropProduceItemId(cropProduceItemId, out string normalizedProduceItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = CropSupplyDataService.GetSupplyScore(normalizedProduceItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(cropDisplayName)
                ? CropSupplyTracker.GetCropDisplayName(normalizedProduceItemId)
                : cropDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Supply modifier for {displayName} ({normalizedProduceItemId}): supply {supplyScore:0.##} vs neutral {CropSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string cropProduceItemId, string? cropDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(cropDisplayName)
                ? CropSupplyTracker.GetCropDisplayName(cropProduceItemId)
                : cropDisplayName;
            float supplyScore = CropSupplyDataService.GetSupplyScore(cropProduceItemId);
            float modifier = GetSellModifier(cropProduceItemId, displayName);
            return $"{displayName} ({cropProduceItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - CropSupplyDataService.NeutralSupplyScore;

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
