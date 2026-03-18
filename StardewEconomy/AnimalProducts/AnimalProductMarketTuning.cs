namespace FarmingCapitalist
{
    /// <summary>
    /// Shared animal-product-side market tuning values, intentionally aligned with crop tuning.
    /// </summary>
    internal static class AnimalProductMarketTuning
    {
        public const float NeutralSupplyScore = CropMarketTuning.NeutralSupplyScore;
        public const float MinSupplyScore = CropMarketTuning.MinSupplyScore;
        public const float MaxSupplyScore = CropMarketTuning.MaxSupplyScore;
        public const float MinimumSellModifier = CropMarketTuning.MinimumSellModifier;
        public const float MaximumSellModifier = CropMarketTuning.MaximumSellModifier;
        public const float OversupplyPenaltyRange = CropMarketTuning.OversupplyPenaltyRange;
        public const float UndersupplyBonusRange = CropMarketTuning.UndersupplyBonusRange;
        public const float ActorDeviationWeightRange = CropMarketTuning.ActorDeviationWeightRange;

        public const float TruffleWinterPriceMultiplier = 1.10f;
        public const float TruffleSummerPriceMultiplier = 0.92f;
        public const float TruffleFallPriceMultiplier = 0.92f;
        public const float TruffleSpringPriceMultiplier = 1f;

        public static readonly float BaseRecoveryRate = CropMarketTuning.BaseRecoveryRate;

        public static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }
    }
}
