using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Save-scoped mineral supply state for mineral-specific supply tracking.
    /// Keys are canonical mineral object item IDs.
    /// </summary>
    public sealed class MineralSupplySaveData
    {
        public Dictionary<string, float> MineralSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
