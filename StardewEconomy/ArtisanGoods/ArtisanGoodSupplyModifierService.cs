using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for artisan-good items.
    /// </summary>
    internal static class ArtisanGoodSupplyModifierService
    {
        private const float OversupplyPenaltyRange = ArtisanGoodMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = ArtisanGoodMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = ArtisanGoodMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = ArtisanGoodMarketTuning.MaximumSellModifier;
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
            if (!ArtisanGoodSupplyTracker.TryGetArtisanGoodInfo(item, out string artisanGoodItemId, out string displayName))
                return 1f;

            return GetSellModifier(artisanGoodItemId, displayName);
        }

        public static float GetSellModifier(string? artisanGoodItemId, string? artisanGoodDisplayName = null)
        {
            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = ArtisanGoodSupplyDataService.GetSupplyScore(normalizedArtisanGoodItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(artisanGoodDisplayName)
                ? ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName(normalizedArtisanGoodItemId)
                : artisanGoodDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Artisan good supply modifier for {displayName} ({normalizedArtisanGoodItemId}): supply {supplyScore:0.##} vs neutral {ArtisanGoodSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string artisanGoodItemId, string? artisanGoodDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(artisanGoodDisplayName)
                ? ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName(artisanGoodItemId)
                : artisanGoodDisplayName;
            float supplyScore = ArtisanGoodSupplyDataService.GetSupplyScore(artisanGoodItemId);
            float modifier = GetSellModifier(artisanGoodItemId, displayName);
            return $"{displayName} ({artisanGoodItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - ArtisanGoodSupplyDataService.NeutralSupplyScore;

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
