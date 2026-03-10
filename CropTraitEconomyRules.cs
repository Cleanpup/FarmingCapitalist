using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Broad crop balancing by crop behavior traits.
    /// This is the first-pass structural compression layer; exact tuning belongs in CropItemEconomyRules.
    /// </summary>
    internal static class CropTraitEconomyRules
    {
        private const float SingleHarvestBuyMultiplier = 1f;
        private const float SingleHarvestSellMultiplier = 1f;
        private const float RegrowthBuyMultiplier = 1f;
        private const float RegrowthSellMultiplier = 1f;

        private const float SingleYieldBuyMultiplier = 1f;
        private const float SingleYieldSellMultiplier = 1f;
        private const float MultiYieldBuyMultiplier = 1f;
        private const float MultiYieldSellMultiplier = 1f;

        private const float FastCropBuyMultiplier = 1f;
        private const float FastCropSellMultiplier = 1f;
        private const float MediumCropBuyMultiplier = 1f;
        private const float MediumCropSellMultiplier = 1f;
        private const float SlowCropBuyMultiplier = 1f;
        private const float SlowCropSellMultiplier = 1f;

        private const float LowHarvestFrequencyBuyMultiplier = 1f;
        private const float LowHarvestFrequencySellMultiplier = 1f;
        private const float MediumHarvestFrequencyBuyMultiplier = 1f;
        private const float MediumHarvestFrequencySellMultiplier = 1f;
        private const float HighHarvestFrequencyBuyMultiplier = 1f;
        private const float HighHarvestFrequencySellMultiplier = 1f;

        private const float CheapSeedBuyMultiplier = 1f;
        private const float CheapSeedSellMultiplier = 1f;
        private const float MidSeedBuyMultiplier = 1f;
        private const float MidSeedSellMultiplier = 1f;
        private const float ExpensiveSeedBuyMultiplier = 1f;
        private const float ExpensiveSeedSellMultiplier = 1f;

        public static float GetBuyTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CropEconomicTrait traits = CropTraitService.GetTraits(item);
            if (traits == CropEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.SingleHarvest, SingleHarvestBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.Regrowth, RegrowthBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.SingleYield, SingleYieldBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.MultiYield, MultiYieldBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.FastCrop, FastCropBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.MediumCrop, MediumCropBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.SlowCrop, SlowCropBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.LowHarvestFrequency, LowHarvestFrequencyBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.MediumHarvestFrequency, MediumHarvestFrequencyBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.HighHarvestFrequency, HighHarvestFrequencyBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.CheapSeed, CheapSeedBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.MidSeed, MidSeedBuyMultiplier);
            modifier *= GetBuyTraitMultiplier(traits, CropEconomicTrait.ExpensiveSeed, ExpensiveSeedBuyMultiplier);
            return modifier;
        }

        public static float GetSellTraitModifier(Item item, EconomyContext context)
        {
            _ = context;

            CropEconomicTrait traits = CropTraitService.GetTraits(item);
            if (traits == CropEconomicTrait.None)
                return 1f;

            float modifier = 1f;
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.SingleHarvest, SingleHarvestSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.Regrowth, RegrowthSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.SingleYield, SingleYieldSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.MultiYield, MultiYieldSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.FastCrop, FastCropSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.MediumCrop, MediumCropSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.SlowCrop, SlowCropSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.LowHarvestFrequency, LowHarvestFrequencySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.MediumHarvestFrequency, MediumHarvestFrequencySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.HighHarvestFrequency, HighHarvestFrequencySellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.CheapSeed, CheapSeedSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.MidSeed, MidSeedSellMultiplier);
            modifier *= GetSellTraitMultiplier(traits, CropEconomicTrait.ExpensiveSeed, ExpensiveSeedSellMultiplier);
            return modifier;
        }

        private static float GetBuyTraitMultiplier(CropEconomicTrait allTraits, CropEconomicTrait trait, float multiplier)
        {
            return (allTraits & trait) == trait
                ? multiplier
                : 1f;
        }

        private static float GetSellTraitMultiplier(CropEconomicTrait allTraits, CropEconomicTrait trait, float multiplier)
        {
            return (allTraits & trait) == trait
                ? multiplier
                : 1f;
        }
    }
}
