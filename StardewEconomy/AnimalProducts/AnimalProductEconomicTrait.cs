using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined animal product traits used by animal-product economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum AnimalProductEconomicTrait
    {
        None = 0,

        Egg = 1 << 0,
        Milk = 1 << 1,
        FiberAnimalProduct = 1 << 2,
        CoopProduct = 1 << 3,
        BarnProduct = 1 << 4,
        SpecialtyAnimalGood = 1 << 5
    }
}
