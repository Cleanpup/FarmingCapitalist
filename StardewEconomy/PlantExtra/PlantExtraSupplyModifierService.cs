using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for plantExtra items.
    /// </summary>
    internal static class PlantExtraSupplyModifierService
    {
        private const float OversupplyPenaltyRange = PlantExtraMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = PlantExtraMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = PlantExtraMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = PlantExtraMarketTuning.MaximumSellModifier;
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
            if (!PlantExtraSupplyTracker.TryGetPlantExtraInfo(item, out string plantExtraItemId, out string displayName))
                return 1f;

            return GetSellModifier(plantExtraItemId, displayName);
        }

        public static float GetSellModifier(string? plantExtraItemId, string? plantExtraDisplayName = null)
        {
            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = PlantExtraSupplyDataService.GetSupplyScore(normalizedPlantExtraItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(plantExtraDisplayName)
                ? PlantExtraSupplyTracker.GetPlantExtraDisplayName(normalizedPlantExtraItemId)
                : plantExtraDisplayName;

            VerbosePriceTraceLogger.Log(
                $"PlantExtra supply modifier for {displayName} ({normalizedPlantExtraItemId}): supply {supplyScore:0.##} vs neutral {PlantExtraSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string plantExtraItemId, string? plantExtraDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(plantExtraDisplayName)
                ? PlantExtraSupplyTracker.GetPlantExtraDisplayName(plantExtraItemId)
                : plantExtraDisplayName;
            float supplyScore = PlantExtraSupplyDataService.GetSupplyScore(plantExtraItemId);
            float modifier = GetSellModifier(plantExtraItemId, displayName);
            return $"{displayName} ({plantExtraItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - PlantExtraSupplyDataService.NeutralSupplyScore;

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
