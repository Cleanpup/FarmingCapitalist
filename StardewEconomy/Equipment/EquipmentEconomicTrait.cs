using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined equipment traits used by equipment economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum EquipmentEconomicTrait
    {
        None = 0,

        Weapon = 1 << 0,
        Ring = 1 << 1,
        Boots = 1 << 2,
        WearableEquipment = 1 << 3
    }
}
