using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static mineral supply tracker.</summary>
    internal sealed class MineralCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            MineralSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            MineralSupplyTracker.TrackMineralSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            MineralSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return MineralSupplyTracker.TryGetMineralInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return MineralSupplyTracker.TryResolveMineralItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return MineralSupplyTracker.TryNormalizeMineralItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return MineralSupplyTracker.GetMineralDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static mineral trait service.</summary>
    internal sealed class MineralCategoryTraitServiceAdapter : ICategoryTraitService<MineralEconomicTrait>
    {
        public MineralEconomicTrait GetTraits(Item? item)
        {
            return MineralTraitService.GetTraits(item);
        }

        public MineralEconomicTrait GetTraits(string? itemId)
        {
            return MineralTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, MineralEconomicTrait trait)
        {
            return MineralTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(MineralEconomicTrait traits)
        {
            return MineralTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return MineralTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            MineralTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static mineral trait economy rules.</summary>
    internal sealed class MineralCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return MineralTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static mineral category registry.</summary>
    internal sealed class MineralEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableMineralEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableMineralEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return MineralEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableMineralEconomyCategoryDefinition definition)
        {
            return MineralEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static mineral actor simulation service.</summary>
    internal sealed class MineralCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<MineralMarketSimulationActorState>
    {
        public List<MineralMarketSimulationActorState> CreateDefaultActorStates()
        {
            return MineralMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<MineralMarketSimulationActorState> NormalizeLoadedActors(
            List<MineralMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return MineralMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
