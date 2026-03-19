namespace FarmingCapitalist
{
    public sealed class CraftingExtraSupplySaveData
    {
        public Dictionary<string, float> CraftingExtraSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
