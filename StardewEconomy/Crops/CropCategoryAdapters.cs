using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static crop supply tracker.</summary>
    internal sealed class CropCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            CropSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            CropSupplyTracker.TrackProduceSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            CropSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return CropSupplyTracker.TryGetCropProduceInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return CropSupplyTracker.TryResolveCropProduceItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return CropSupplyTracker.TryNormalizeCropProduceItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return CropSupplyTracker.GetCropDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static crop trait service.</summary>
    internal sealed class CropCategoryTraitServiceAdapter : ICategoryTraitService<CropEconomicTrait>
    {
        public CropEconomicTrait GetTraits(Item? item)
        {
            return CropTraitService.GetTraits(item);
        }

        public CropEconomicTrait GetTraits(string? itemId)
        {
            return CropTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, CropEconomicTrait trait)
        {
            return CropTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(CropEconomicTrait traits)
        {
            return CropTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return CropTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            CropTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static crop trait economy rules.</summary>
    internal sealed class CropCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return CropTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static crop category registry.</summary>
    internal sealed class CropEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableCropEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableCropEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return CropEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableCropEconomyCategoryDefinition definition)
        {
            return CropEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static crop actor simulation service.</summary>
    internal sealed class CropCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<CropMarketSimulationActorState>
    {
        public List<CropMarketSimulationActorState> CreateDefaultActorStates()
        {
            return CropMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<CropMarketSimulationActorState> NormalizeLoadedActors(
            List<CropMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return CropMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
