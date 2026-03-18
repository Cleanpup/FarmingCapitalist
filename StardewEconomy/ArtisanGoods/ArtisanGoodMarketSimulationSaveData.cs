namespace FarmingCapitalist
{
    public sealed class ArtisanGoodMarketSimulationSaveData : ICategoryMarketSimulationSaveData<ArtisanGoodMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<ArtisanGoodMarketSimulationActorState> Actors { get; set; } = new();
    }
}
