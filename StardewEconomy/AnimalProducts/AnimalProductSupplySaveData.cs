using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Save-scoped animal product supply state for animal-product-specific supply tracking.
    /// Keys are canonical direct-output object item IDs.
    /// </summary>
    public sealed class AnimalProductSupplySaveData
    {
        public Dictionary<string, float> AnimalProductSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
