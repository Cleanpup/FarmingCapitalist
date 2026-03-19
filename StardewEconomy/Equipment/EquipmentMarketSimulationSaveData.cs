namespace FarmingCapitalist
{
    public sealed class EquipmentMarketSimulationSaveData : ICategoryMarketSimulationSaveData<EquipmentMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<EquipmentMarketSimulationActorState> Actors { get; set; } = new();
    }
}
