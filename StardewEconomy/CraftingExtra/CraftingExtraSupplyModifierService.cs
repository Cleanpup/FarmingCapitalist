using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for crafting-extra items.
    /// </summary>
    internal static class CraftingExtraSupplyModifierService
    {
        private const float OversupplyPenaltyRange = CraftingExtraMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = CraftingExtraMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = CraftingExtraMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = CraftingExtraMarketTuning.MaximumSellModifier;
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
            if (!CraftingExtraSupplyTracker.TryGetCraftingExtraInfo(item, out string craftingExtraItemId, out string displayName))
                return 1f;

            return GetSellModifier(craftingExtraItemId, displayName);
        }

        public static float GetSellModifier(string? craftingExtraItemId, string? craftingExtraDisplayName = null)
        {
            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = CraftingExtraSupplyDataService.GetSupplyScore(normalizedCraftingExtraItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(craftingExtraDisplayName)
                ? CraftingExtraSupplyTracker.GetCraftingExtraDisplayName(normalizedCraftingExtraItemId)
                : craftingExtraDisplayName;

            VerbosePriceTraceLogger.Log(
                $"CraftingExtra supply modifier for {displayName} ({normalizedCraftingExtraItemId}): supply {supplyScore:0.##} vs neutral {CraftingExtraSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string craftingExtraItemId, string? craftingExtraDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(craftingExtraDisplayName)
                ? CraftingExtraSupplyTracker.GetCraftingExtraDisplayName(craftingExtraItemId)
                : craftingExtraDisplayName;
            float supplyScore = CraftingExtraSupplyDataService.GetSupplyScore(craftingExtraItemId);
            float modifier = GetSellModifier(craftingExtraItemId, displayName);
            return $"{displayName} ({craftingExtraItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - CraftingExtraSupplyDataService.NeutralSupplyScore;

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
