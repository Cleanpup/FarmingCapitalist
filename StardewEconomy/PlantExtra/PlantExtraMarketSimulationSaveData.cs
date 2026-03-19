namespace FarmingCapitalist
{
    public sealed class PlantExtraMarketSimulationSaveData : ICategoryMarketSimulationSaveData<PlantExtraMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<PlantExtraMarketSimulationActorState> Actors { get; set; } = new();
    }
}
