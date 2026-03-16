namespace FarmingCapitalist
{
    /// <summary>
    /// Shared fish-side market tuning values so tracking, simulation, and pricing stay on the same scale.
    /// </summary>
    internal static class FishMarketTuning
    {
        public const float NeutralSupplyScore = 30f;
        public const float MinSupplyScore = 0f;
        public const float MaxSupplyScore = 60f;
        public const float MinimumSellModifier = 0.60f;
        public const float MaximumSellModifier = 1.15f;
        public const float OversupplyPenaltyRange = 75f;
        public const float UndersupplyBonusRange = 200f;
        public const float MeanReversionSnapThreshold = 0.5f;
        public const float BaseSeasonalSupplyStrength = 0.16f;
        public const float ActorDeviationWeightRange = 60f;

        // A 13.6% daily pull reduces a 30-point deviation to the snap threshold in 28 days.
        public static readonly float BaseRecoveryRate = 0.13603809f;

        public static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }
    }
}
