namespace FarmingCapitalist
{
    public sealed class MonsterLootSupplySaveData
    {
        public Dictionary<string, float> MonsterLootSupplyScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
