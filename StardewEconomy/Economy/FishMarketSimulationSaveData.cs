namespace FarmingCapitalist
{
    public sealed class FishMarketSimulationSaveData
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<FishMarketSimulationActorState> Actors { get; set; } = new();
    }
}
