using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined artisan-good traits used by artisan-good economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum ArtisanGoodEconomicTrait
    {
        None = 0,

        AlcoholBeverage = 1 << 0,
        Preserve = 1 << 1,
        DairyArtisanGood = 1 << 2,
        ClothLoomProduct = 1 << 3,
        OilProduct = 1 << 4,
        SpecialtyProcessedGood = 1 << 5
    }
}
