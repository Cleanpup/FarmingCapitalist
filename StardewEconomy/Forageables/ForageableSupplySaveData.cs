namespace FarmingCapitalist
{
    public sealed class ForageableSupplySaveData
    {
        public Dictionary<string, float> ForageableSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
