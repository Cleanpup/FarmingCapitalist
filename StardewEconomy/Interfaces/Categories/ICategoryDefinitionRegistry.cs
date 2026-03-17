namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared read-only access pattern for randomized category registries.
    /// </summary>
    internal interface ICategoryDefinitionRegistry<TDefinition>
    {
        /// <summary>Get the full ordered set of randomizable category definitions.</summary>
        IReadOnlyList<TDefinition> GetRandomizableCategories();

        /// <summary>Try get a randomizable category definition by key.</summary>
        bool TryGetCategory(string? key, out TDefinition definition);
    }
}
