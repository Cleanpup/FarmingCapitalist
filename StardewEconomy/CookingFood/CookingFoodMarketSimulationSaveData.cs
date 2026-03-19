namespace FarmingCapitalist
{
    public sealed class CookingFoodMarketSimulationSaveData : ICategoryMarketSimulationSaveData<CookingFoodMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<CookingFoodMarketSimulationActorState> Actors { get; set; } = new();
    }
}
