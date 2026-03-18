using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for forageable items.
    /// </summary>
    internal static class ForageableSupplyModifierService
    {
        private const float OversupplyPenaltyRange = ForageableMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = ForageableMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = ForageableMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = ForageableMarketTuning.MaximumSellModifier;
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
            if (!ForageableSupplyTracker.TryGetForageableInfo(item, out string forageableItemId, out string displayName))
                return 1f;

            return GetSellModifier(forageableItemId, displayName);
        }

        public static float GetSellModifier(string? forageableItemId, string? forageableDisplayName = null)
        {
            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = ForageableSupplyDataService.GetSupplyScore(normalizedForageableItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(forageableDisplayName)
                ? ForageableSupplyTracker.GetForageableDisplayName(normalizedForageableItemId)
                : forageableDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Forageable supply modifier for {displayName} ({normalizedForageableItemId}): supply {supplyScore:0.##} vs neutral {ForageableSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string forageableItemId, string? forageableDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(forageableDisplayName)
                ? ForageableSupplyTracker.GetForageableDisplayName(forageableItemId)
                : forageableDisplayName;
            float supplyScore = ForageableSupplyDataService.GetSupplyScore(forageableItemId);
            float modifier = GetSellModifier(forageableItemId, displayName);
            return $"{displayName} ({forageableItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - ForageableSupplyDataService.NeutralSupplyScore;

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
