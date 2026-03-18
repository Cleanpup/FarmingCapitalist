namespace FarmingCapitalist
{
    public sealed class MonsterLootMarketSimulationSaveData : ICategoryMarketSimulationSaveData<MonsterLootMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<MonsterLootMarketSimulationActorState> Actors { get; set; } = new();
    }
}
