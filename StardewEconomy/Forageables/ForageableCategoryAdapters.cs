using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static forageable supply tracker.</summary>
    internal sealed class ForageableCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            ForageableSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            ForageableSupplyTracker.TrackForageableSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            ForageableSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return ForageableSupplyTracker.TryGetForageableInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return ForageableSupplyTracker.TryResolveForageableItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return ForageableSupplyTracker.TryNormalizeForageableItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return ForageableSupplyTracker.GetForageableDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static forageable trait service.</summary>
    internal sealed class ForageableCategoryTraitServiceAdapter : ICategoryTraitService<ForageableEconomicTrait>
    {
        public ForageableEconomicTrait GetTraits(Item? item)
        {
            return ForageableTraitService.GetTraits(item);
        }

        public ForageableEconomicTrait GetTraits(string? itemId)
        {
            return ForageableTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, ForageableEconomicTrait trait)
        {
            return ForageableTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(ForageableEconomicTrait traits)
        {
            return ForageableTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return ForageableTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            ForageableTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static forageable trait economy rules.</summary>
    internal sealed class ForageableCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return ForageableTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static forageable category registry.</summary>
    internal sealed class ForageableEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableForageableEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableForageableEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return ForageableEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableForageableEconomyCategoryDefinition definition)
        {
            return ForageableEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static forageable actor simulation service.</summary>
    internal sealed class ForageableCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<ForageableMarketSimulationActorState>
    {
        public List<ForageableMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ForageableMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<ForageableMarketSimulationActorState> NormalizeLoadedActors(
            List<ForageableMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return ForageableMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
