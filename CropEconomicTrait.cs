using System;

namespace FarmingCapitalist // holds enum values for new crop type/categories
{
    /// <summary>
    /// Mod-defined crop behavior traits used by FarmingCapitalist economic systems.
    /// These are separate from vanilla item categories and are based on crop mechanics.
    /// </summary>
    [Flags]
    internal enum CropEconomicTrait
    {
        None = 0,

        SingleHarvest = 1 << 0,
        Regrowth = 1 << 1,

        SingleYield = 1 << 2,
        MultiYield = 1 << 3,

        FastCrop = 1 << 4,
        MediumCrop = 1 << 5,
        SlowCrop = 1 << 6,

        CheapSeed = 1 << 7,
        MidSeed = 1 << 8,
        ExpensiveSeed = 1 << 9,

        LowHarvestFrequency = 1 << 10,
        MediumHarvestFrequency = 1 << 11,
        HighHarvestFrequency = 1 << 12
    }
}
