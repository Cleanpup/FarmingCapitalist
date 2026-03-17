using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static fish supply tracker.</summary>
    internal sealed class FishCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            FishSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            FishSupplyTracker.TrackFishSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            FishSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return FishSupplyTracker.TryGetFishInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return FishSupplyTracker.TryResolveFishItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return FishSupplyTracker.TryNormalizeFishItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return FishSupplyTracker.GetFishDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static fish trait service.</summary>
    internal sealed class FishCategoryTraitServiceAdapter : ICategoryTraitService<FishEconomicTrait>
    {
        public FishEconomicTrait GetTraits(Item? item)
        {
            return FishTraitService.GetTraits(item);
        }

        public FishEconomicTrait GetTraits(string? itemId)
        {
            return FishTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, FishEconomicTrait trait)
        {
            return FishTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(FishEconomicTrait traits)
        {
            return FishTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return FishTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            FishTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static fish trait economy rules.</summary>
    internal sealed class FishCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return FishTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static fish category registry.</summary>
    internal sealed class FishEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableFishEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableFishEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return FishEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableFishEconomyCategoryDefinition definition)
        {
            return FishEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static fish actor simulation service.</summary>
    internal sealed class FishCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<FishMarketSimulationActorState>
    {
        public List<FishMarketSimulationActorState> CreateDefaultActorStates()
        {
            return FishMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<FishMarketSimulationActorState> NormalizeLoadedActors(
            List<FishMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return FishMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
