using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static crafting-extra supply tracker.</summary>
    internal sealed class CraftingExtraCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            CraftingExtraSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            CraftingExtraSupplyTracker.TrackCraftingExtraSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            CraftingExtraSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return CraftingExtraSupplyTracker.TryGetCraftingExtraInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return CraftingExtraSupplyTracker.TryResolveCraftingExtraItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return CraftingExtraSupplyTracker.TryNormalizeCraftingExtraItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return CraftingExtraSupplyTracker.GetCraftingExtraDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static crafting-extra trait service.</summary>
    internal sealed class CraftingExtraCategoryTraitServiceAdapter : ICategoryTraitService<CraftingExtraEconomicTrait>
    {
        public CraftingExtraEconomicTrait GetTraits(Item? item)
        {
            return CraftingExtraTraitService.GetTraits(item);
        }

        public CraftingExtraEconomicTrait GetTraits(string? itemId)
        {
            return CraftingExtraTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, CraftingExtraEconomicTrait trait)
        {
            return CraftingExtraTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(CraftingExtraEconomicTrait traits)
        {
            return CraftingExtraTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return CraftingExtraTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            CraftingExtraTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static crafting-extra trait economy rules.</summary>
    internal sealed class CraftingExtraCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return CraftingExtraTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static crafting-extra category registry.</summary>
    internal sealed class CraftingExtraEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableCraftingExtraEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableCraftingExtraEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return CraftingExtraEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableCraftingExtraEconomyCategoryDefinition definition)
        {
            return CraftingExtraEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static crafting-extra actor simulation service.</summary>
    internal sealed class CraftingExtraCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<CraftingExtraMarketSimulationActorState>
    {
        public List<CraftingExtraMarketSimulationActorState> CreateDefaultActorStates()
        {
            return CraftingExtraMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<CraftingExtraMarketSimulationActorState> NormalizeLoadedActors(
            List<CraftingExtraMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return CraftingExtraMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
