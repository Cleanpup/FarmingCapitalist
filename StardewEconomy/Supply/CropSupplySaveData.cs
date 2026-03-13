using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Save-scoped crop supply state used by the first supply/demand pass.
    /// Keys are canonical crop produce item IDs, not seed IDs.
    /// </summary>
    public sealed class CropSupplySaveData
    {
        public Dictionary<string, float> CropSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int LastDecayDay { get; set; } = -1;
    }
}
