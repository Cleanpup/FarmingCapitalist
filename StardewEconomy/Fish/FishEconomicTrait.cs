using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined fish behavior traits used by fish economy systems.
    /// These are broad buckets derived from vanilla fish data, not per-location spawn rules.
    /// </summary>
    [Flags]
    internal enum FishEconomicTrait
    {
        None = 0,

        Spring = 1 << 0,
        Summer = 1 << 1,
        Fall = 1 << 2,
        Winter = 1 << 3,

        Morning = 1 << 4,
        Day = 1 << 5,
        Evening = 1 << 6,
        Night = 1 << 7,

        Sunny = 1 << 8,
        Rainy = 1 << 9,

        Trap = 1 << 10,
        LineCaught = 1 << 11
    }
}
