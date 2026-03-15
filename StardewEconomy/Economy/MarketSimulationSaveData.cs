using System;

namespace FarmingCapitalist
{
    public sealed class MarketSimulationSaveData
    {
        public int LastSimulationDay { get; set; } = -1;
        public List<MarketSimulationActorState> Actors { get; set; } = new();
    }
}
