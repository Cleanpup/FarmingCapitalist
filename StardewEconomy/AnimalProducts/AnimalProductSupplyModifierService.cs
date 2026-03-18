using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for animal product items.
    /// </summary>
    internal static class AnimalProductSupplyModifierService
    {
        private const float OversupplyPenaltyRange = AnimalProductMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = AnimalProductMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = AnimalProductMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = AnimalProductMarketTuning.MaximumSellModifier;
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
            if (!AnimalProductSupplyTracker.TryGetAnimalProductInfo(item, out string animalProductItemId, out string displayName))
                return 1f;

            return GetSellModifier(animalProductItemId, displayName);
        }

        public static float GetSellModifier(string? animalProductItemId, string? animalProductDisplayName = null)
        {
            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = AnimalProductSupplyDataService.GetSupplyScore(normalizedAnimalProductItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(animalProductDisplayName)
                ? AnimalProductSupplyTracker.GetAnimalProductDisplayName(normalizedAnimalProductItemId)
                : animalProductDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Animal product supply modifier for {displayName} ({normalizedAnimalProductItemId}): supply {supplyScore:0.##} vs neutral {AnimalProductSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string animalProductItemId, string? animalProductDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(animalProductDisplayName)
                ? AnimalProductSupplyTracker.GetAnimalProductDisplayName(animalProductItemId)
                : animalProductDisplayName;
            float supplyScore = AnimalProductSupplyDataService.GetSupplyScore(animalProductItemId);
            float modifier = GetSellModifier(animalProductItemId, displayName);
            return $"{displayName} ({animalProductItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - AnimalProductSupplyDataService.NeutralSupplyScore;

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
