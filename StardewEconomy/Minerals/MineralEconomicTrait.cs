using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined mineral behavior traits used by mineral economy systems.
    /// These are broad rarity and value buckets derived from vanilla mineral data.
    /// </summary>
    [Flags]
    internal enum MineralEconomicTrait
    {
        None = 0,

        Common = 1 << 0,
        Uncommon = 1 << 1,
        Rare = 1 << 2,
        Luxury = 1 << 3
    }
}
