namespace FarmingCapitalist
{
    /// <summary>
    /// Shared crop-side market tuning values so supply, simulation, and pricing stay on the same scale.
    /// </summary>
    internal static class CropMarketTuning
    {
        public const float NeutralSupplyScore = 100f;
        public const float MinSupplyScore = 20f;
        public const float MaxSupplyScore = 300f;
        public const float MinimumSellModifier = 0.60f;
        public const float MaximumSellModifier = 1.15f;
        public const float OversupplyPenaltyRange = 1000f;
        public const float UndersupplyBonusRange = 666.67f;
        public const float BaseSeasonalDemandStrength = 0.75f;
        public const float ActorDeviationWeightRange = 300f;

        // A 12.4% daily pull reduces a 200-point surplus to roughly 5 points over 28 days.
        public static readonly float BaseRecoveryRate = 0.124f;

        public static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }
    }
}
