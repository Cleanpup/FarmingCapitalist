using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Internal crafting-extra trait flags.
    /// This category intentionally has no user-facing subcategories, so every eligible item resolves to the same trait.
    /// </summary>
    [Flags]
    internal enum CraftingExtraEconomicTrait
    {
        None = 0,

        Material = 1 << 0
    }
}
