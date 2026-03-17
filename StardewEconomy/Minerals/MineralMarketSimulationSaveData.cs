namespace FarmingCapitalist
{
    public sealed class MineralMarketSimulationSaveData : ICategoryMarketSimulationSaveData<MineralMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<MineralMarketSimulationActorState> Actors { get; set; } = new();
    }
}
