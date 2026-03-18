namespace FarmingCapitalist
{
    public sealed class ForageableMarketSimulationSaveData : ICategoryMarketSimulationSaveData<ForageableMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<ForageableMarketSimulationActorState> Actors { get; set; } = new();
    }
}
