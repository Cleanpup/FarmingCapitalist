namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared factory and normalization contract for category actor simulation state.
    /// </summary>
    internal interface ICategoryMarketActorSimulationService<TActorState>
    {
        /// <summary>Create the default persistent actor-state set for a category.</summary>
        List<TActorState> CreateDefaultActorStates();

        /// <summary>Normalize loaded actor state and report whether the result should be persisted.</summary>
        List<TActorState> NormalizeLoadedActors(List<TActorState>? loadedActors, out bool shouldPersist);
    }
}
