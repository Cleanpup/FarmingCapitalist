using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static animal product supply tracker.</summary>
    internal sealed class AnimalProductCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            AnimalProductSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            AnimalProductSupplyTracker.TrackAnimalProductSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            AnimalProductSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return AnimalProductSupplyTracker.TryGetAnimalProductInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return AnimalProductSupplyTracker.TryResolveAnimalProductItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return AnimalProductSupplyTracker.TryNormalizeAnimalProductItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return AnimalProductSupplyTracker.GetAnimalProductDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static animal product trait service.</summary>
    internal sealed class AnimalProductCategoryTraitServiceAdapter : ICategoryTraitService<AnimalProductEconomicTrait>
    {
        public AnimalProductEconomicTrait GetTraits(Item? item)
        {
            return AnimalProductTraitService.GetTraits(item);
        }

        public AnimalProductEconomicTrait GetTraits(string? itemId)
        {
            return AnimalProductTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, AnimalProductEconomicTrait trait)
        {
            return AnimalProductTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(AnimalProductEconomicTrait traits)
        {
            return AnimalProductTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return AnimalProductTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            AnimalProductTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static animal product trait economy rules.</summary>
    internal sealed class AnimalProductCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return AnimalProductTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static animal product category registry.</summary>
    internal sealed class AnimalProductEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableAnimalProductEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableAnimalProductEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return AnimalProductEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableAnimalProductEconomyCategoryDefinition definition)
        {
            return AnimalProductEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static animal product actor simulation service.</summary>
    internal sealed class AnimalProductCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<AnimalProductMarketSimulationActorState>
    {
        public List<AnimalProductMarketSimulationActorState> CreateDefaultActorStates()
        {
            return AnimalProductMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<AnimalProductMarketSimulationActorState> NormalizeLoadedActors(
            List<AnimalProductMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return AnimalProductMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
