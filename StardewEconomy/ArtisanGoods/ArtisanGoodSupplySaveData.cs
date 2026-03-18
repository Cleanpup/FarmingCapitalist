namespace FarmingCapitalist
{
    public sealed class ArtisanGoodSupplySaveData
    {
        public Dictionary<string, float> ArtisanGoodSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
