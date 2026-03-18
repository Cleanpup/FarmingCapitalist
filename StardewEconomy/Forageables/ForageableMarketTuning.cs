namespace FarmingCapitalist
{
    /// <summary>
    /// Shared forageable-side market tuning values, aligned with crop tuning.
    /// </summary>
    internal static class ForageableMarketTuning
    {
        public const float NeutralSupplyScore = CropMarketTuning.NeutralSupplyScore;
        public const float MinSupplyScore = CropMarketTuning.MinSupplyScore;
        public const float MaxSupplyScore = CropMarketTuning.MaxSupplyScore;
        public const float MinimumSellModifier = CropMarketTuning.MinimumSellModifier;
        public const float MaximumSellModifier = CropMarketTuning.MaximumSellModifier;
        public const float OversupplyPenaltyRange = CropMarketTuning.OversupplyPenaltyRange;
        public const float UndersupplyBonusRange = CropMarketTuning.UndersupplyBonusRange;
        public const float ActorDeviationWeightRange = CropMarketTuning.ActorDeviationWeightRange;
        public const float BaseSeasonalSupplyStrength = CropMarketTuning.BaseSeasonalDemandStrength;

        public const float InSeasonPriceMultiplier = 0.92f;
        public const float OutOfSeasonPriceMultiplier = 1.08f;
        public const float YearRoundPriceMultiplier = 1f;

        public static readonly float BaseRecoveryRate = CropMarketTuning.BaseRecoveryRate;

        public static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }
    }
}
