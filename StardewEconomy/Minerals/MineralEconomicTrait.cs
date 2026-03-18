using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined mining behavior traits used by the mineral-owned economy systems.
    /// These are mine-material buckets derived from vanilla categories, tags, and geode behavior.
    /// </summary>
    [Flags]
    internal enum MineralEconomicTrait
    {
        None = 0,

        Stone = 1 << 0,
        Coal = 1 << 1,
        Ore = 1 << 2,
        Bar = 1 << 3,
        Mineral = 1 << 4,
        Gem = 1 << 5,
        Geode = 1 << 6
    }
}
