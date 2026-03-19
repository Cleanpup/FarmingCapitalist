namespace FarmingCapitalist
{
    public sealed class PlantExtraSupplySaveData
    {
        public Dictionary<string, float> PlantExtraSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
