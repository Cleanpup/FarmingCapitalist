namespace FarmingCapitalist
{
    public sealed class CookingFoodSupplySaveData
    {
        public Dictionary<string, float> CookingFoodSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
