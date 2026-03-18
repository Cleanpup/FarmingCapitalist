using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined monster-loot traits used by monster-loot economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum MonsterLootEconomicTrait
    {
        None = 0,

        BasicMonsterDrop = 1 << 0,
        SlimeRelatedItem = 1 << 1,
        EssenceMagicalDrop = 1 << 2
    }
}
