using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined forageable traits used by forageable economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum ForageableEconomicTrait
    {
        None = 0,

        SeasonalForage = 1 << 0,
        BeachForage = 1 << 1,
        ForestForage = 1 << 2,
        DesertForage = 1 << 3,
        GingerIslandForage = 1 << 4,
        GatheredFlowersWildEdibles = 1 << 5
    }
}
