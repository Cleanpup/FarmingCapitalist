using System;

namespace FarmingCapitalist
{
    public sealed class CropMarketSimulationSaveData : ICategoryMarketSimulationSaveData<CropMarketSimulationActorState>
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<CropMarketSimulationActorState> Actors { get; set; } = new();
    }
}
