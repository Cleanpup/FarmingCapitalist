using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for cooking-food items.
    /// </summary>
    internal static class CookingFoodSupplyModifierService
    {
        private const float OversupplyPenaltyRange = CookingFoodMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = CookingFoodMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = CookingFoodMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = CookingFoodMarketTuning.MaximumSellModifier;
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
            if (!CookingFoodSupplyTracker.TryGetCookingFoodInfo(item, out string cookingFoodItemId, out string displayName))
                return 1f;

            return GetSellModifier(cookingFoodItemId, displayName);
        }

        public static float GetSellModifier(string? cookingFoodItemId, string? cookingFoodDisplayName = null)
        {
            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = CookingFoodSupplyDataService.GetSupplyScore(normalizedCookingFoodItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(cookingFoodDisplayName)
                ? CookingFoodSupplyTracker.GetCookingFoodDisplayName(normalizedCookingFoodItemId)
                : cookingFoodDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Cooking food supply modifier for {displayName} ({normalizedCookingFoodItemId}): supply {supplyScore:0.##} vs neutral {CookingFoodSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string cookingFoodItemId, string? cookingFoodDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(cookingFoodDisplayName)
                ? CookingFoodSupplyTracker.GetCookingFoodDisplayName(cookingFoodItemId)
                : cookingFoodDisplayName;
            float supplyScore = CookingFoodSupplyDataService.GetSupplyScore(cookingFoodItemId);
            float modifier = GetSellModifier(cookingFoodItemId, displayName);
            return $"{displayName} ({cookingFoodItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - CookingFoodSupplyDataService.NeutralSupplyScore;

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
