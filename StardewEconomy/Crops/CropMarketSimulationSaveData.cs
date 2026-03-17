using System;

namespace FarmingCapitalist
{
    public sealed class CropMarketSimulationSaveData
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<CropMarketSimulationActorState> Actors { get; set; } = new();
    }
}
