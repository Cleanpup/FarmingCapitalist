using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for mining items.
    /// </summary>
    internal static class MineralSupplyModifierService
    {
        private const float OversupplyPenaltyRange = MineralMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = MineralMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = MineralMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = MineralMarketTuning.MaximumSellModifier;
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
            if (!MineralSupplyTracker.TryGetMineralInfo(item, out string mineralItemId, out string displayName))
                return 1f;

            return GetSellModifier(mineralItemId, displayName);
        }

        public static float GetSellModifier(string? mineralItemId, string? mineralDisplayName = null)
        {
            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = MineralSupplyDataService.GetSupplyScore(normalizedMineralItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(mineralDisplayName)
                ? MineralSupplyTracker.GetMineralDisplayName(normalizedMineralItemId)
                : mineralDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Mining supply modifier for {displayName} ({normalizedMineralItemId}): supply {supplyScore:0.##} vs neutral {MineralSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string mineralItemId, string? mineralDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(mineralDisplayName)
                ? MineralSupplyTracker.GetMineralDisplayName(mineralItemId)
                : mineralDisplayName;
            float supplyScore = MineralSupplyDataService.GetSupplyScore(mineralItemId);
            float modifier = GetSellModifier(mineralItemId, displayName);
            return $"{displayName} ({mineralItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - MineralSupplyDataService.NeutralSupplyScore;

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
