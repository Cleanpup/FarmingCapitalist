using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined plant-extra traits used by plant-extra economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum PlantExtraEconomicTrait
    {
        None = 0,

        TreeFruit = 1 << 0,
        TreeSapling = 1 << 1,
        Flower = 1 << 2,
        FlowerSeedSpecialSeed = 1 << 3,
        Mushroom = 1 << 4,
        TappedProduct = 1 << 5,
        Fertilizer = 1 << 6
    }
}
