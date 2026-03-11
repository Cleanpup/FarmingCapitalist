namespace FarmingCapitalist
{
    /// <summary>
    /// Tunable generation settings for save-specific market profiles.
    /// </summary>
    internal sealed class EconomyProfileGenerationSettings
    {
        public int BonusCategoryCount { get; init; } = 2;
        public int NerfCategoryCount { get; init; } = 2;

        public float BonusSellMultiplier { get; init; } = 1.15f;
        public float NerfSellMultiplier { get; init; } = 0.85f;

        public bool RandomizeBuyMultipliers { get; init; } = false;
        public float BonusBuyMultiplier { get; init; } = 1f;
        public float NerfBuyMultiplier { get; init; } = 1f;
    }
}
