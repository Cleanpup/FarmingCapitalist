using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Save-scoped fish supply state for fish-specific supply tracking.
    /// Keys are canonical fish object item IDs.
    /// </summary>
    public sealed class FishSupplySaveData
    {
        public Dictionary<string, float> FishSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
