namespace FarmingCapitalist
{
    public sealed class AnimalProductMarketSimulationSaveData : ICategoryMarketSimulationSaveData<AnimalProductMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<AnimalProductMarketSimulationActorState> Actors { get; set; } = new();
    }
}
