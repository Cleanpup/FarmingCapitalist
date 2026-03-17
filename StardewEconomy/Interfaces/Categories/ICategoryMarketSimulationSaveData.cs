namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared save-data contract for category market simulation state.
    /// </summary>
    internal interface ICategoryMarketSimulationSaveData<TActorState>
    {
        /// <summary>Get or set the last simulated in-game day key.</summary>
        int LastSimulationDay { get; set; }

        /// <summary>Get or set the persisted actor-state collection.</summary>
        List<TActorState> Actors { get; set; }
    }
}
