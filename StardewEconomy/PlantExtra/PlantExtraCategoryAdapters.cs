using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static plantExtra supply tracker.</summary>
    internal sealed class PlantExtraCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            PlantExtraSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            PlantExtraSupplyTracker.TrackPlantExtraSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            PlantExtraSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return PlantExtraSupplyTracker.TryGetPlantExtraInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return PlantExtraSupplyTracker.TryResolvePlantExtraItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return PlantExtraSupplyTracker.TryNormalizePlantExtraItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return PlantExtraSupplyTracker.GetPlantExtraDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static plantExtra trait service.</summary>
    internal sealed class PlantExtraCategoryTraitServiceAdapter : ICategoryTraitService<PlantExtraEconomicTrait>
    {
        public PlantExtraEconomicTrait GetTraits(Item? item)
        {
            return PlantExtraTraitService.GetTraits(item);
        }

        public PlantExtraEconomicTrait GetTraits(string? itemId)
        {
            return PlantExtraTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, PlantExtraEconomicTrait trait)
        {
            return PlantExtraTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(PlantExtraEconomicTrait traits)
        {
            return PlantExtraTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return PlantExtraTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            PlantExtraTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static plantExtra trait economy rules.</summary>
    internal sealed class PlantExtraCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return PlantExtraTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static plantExtra category registry.</summary>
    internal sealed class PlantExtraEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizablePlantExtraEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizablePlantExtraEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return PlantExtraEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizablePlantExtraEconomyCategoryDefinition definition)
        {
            return PlantExtraEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static plantExtra actor simulation service.</summary>
    internal sealed class PlantExtraCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<PlantExtraMarketSimulationActorState>
    {
        public List<PlantExtraMarketSimulationActorState> CreateDefaultActorStates()
        {
            return PlantExtraMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<PlantExtraMarketSimulationActorState> NormalizeLoadedActors(
            List<PlantExtraMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return PlantExtraMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
