namespace FarmingCapitalist
{
    public sealed class EquipmentSupplySaveData
    {
        public Dictionary<string, float> EquipmentSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
