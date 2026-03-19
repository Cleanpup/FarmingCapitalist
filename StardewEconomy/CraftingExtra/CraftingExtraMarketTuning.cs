namespace FarmingCapitalist
{
    /// <summary>
    /// Shared crafting-extra market tuning values, aligned with crop tuning.
    /// </summary>
    internal static class CraftingExtraMarketTuning
    {
        public const float NeutralSupplyScore = CropMarketTuning.NeutralSupplyScore;
        public const float MinSupplyScore = CropMarketTuning.MinSupplyScore;
        public const float MaxSupplyScore = CropMarketTuning.MaxSupplyScore;
        public const float MinimumSellModifier = CropMarketTuning.MinimumSellModifier;
        public const float MaximumSellModifier = CropMarketTuning.MaximumSellModifier;
        public const float OversupplyPenaltyRange = CropMarketTuning.OversupplyPenaltyRange;
        public const float UndersupplyBonusRange = CropMarketTuning.UndersupplyBonusRange;
        public const float ActorDeviationWeightRange = CropMarketTuning.ActorDeviationWeightRange;

        public static readonly float BaseRecoveryRate = CropMarketTuning.BaseRecoveryRate;

        public static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }
    }
}
