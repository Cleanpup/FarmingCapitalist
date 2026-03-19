namespace FarmingCapitalist
{
    public sealed class CraftingExtraMarketSimulationSaveData : ICategoryMarketSimulationSaveData<CraftingExtraMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<CraftingExtraMarketSimulationActorState> Actors { get; set; } = new();
    }
}
