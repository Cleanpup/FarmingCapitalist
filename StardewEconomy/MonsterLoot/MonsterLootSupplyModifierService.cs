using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Converts tracked supply into a sell-price modifier for monster-loot items.
    /// </summary>
    internal static class MonsterLootSupplyModifierService
    {
        private const float OversupplyPenaltyRange = MonsterLootMarketTuning.OversupplyPenaltyRange;
        private const float UndersupplyBonusRange = MonsterLootMarketTuning.UndersupplyBonusRange;
        private const float MinimumSellModifier = MonsterLootMarketTuning.MinimumSellModifier;
        private const float MaximumSellModifier = MonsterLootMarketTuning.MaximumSellModifier;
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
            if (!MonsterLootSupplyTracker.TryGetMonsterLootInfo(item, out string monsterLootItemId, out string displayName))
                return 1f;

            return GetSellModifier(monsterLootItemId, displayName);
        }

        public static float GetSellModifier(string? monsterLootItemId, string? monsterLootDisplayName = null)
        {
            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return 1f;

            if (TryGetDebugSellModifierOverride(out float debugOverride))
                return debugOverride;

            float supplyScore = MonsterLootSupplyDataService.GetSupplyScore(normalizedMonsterLootItemId);
            float modifier = CalculateModifier(supplyScore);
            string displayName = string.IsNullOrWhiteSpace(monsterLootDisplayName)
                ? MonsterLootSupplyTracker.GetMonsterLootDisplayName(normalizedMonsterLootItemId)
                : monsterLootDisplayName;

            VerbosePriceTraceLogger.Log(
                $"Monster loot supply modifier for {displayName} ({normalizedMonsterLootItemId}): supply {supplyScore:0.##} vs neutral {MonsterLootSupplyDataService.NeutralSupplyScore:0.##} -> x{modifier:0.###}"
            );

            return modifier;
        }

        public static string GetDebugSummary(string monsterLootItemId, string? monsterLootDisplayName = null)
        {
            string displayName = string.IsNullOrWhiteSpace(monsterLootDisplayName)
                ? MonsterLootSupplyTracker.GetMonsterLootDisplayName(monsterLootItemId)
                : monsterLootDisplayName;
            float supplyScore = MonsterLootSupplyDataService.GetSupplyScore(monsterLootItemId);
            float modifier = GetSellModifier(monsterLootItemId, displayName);
            return $"{displayName} ({monsterLootItemId}): supply {supplyScore:0.##}, modifier x{modifier:0.###}";
        }

        private static float CalculateModifier(float supplyScore)
        {
            float unclampedModifier = 1f;
            float deltaFromNeutral = supplyScore - MonsterLootSupplyDataService.NeutralSupplyScore;

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
