using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static equipment supply tracker.</summary>
    internal sealed class EquipmentCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            EquipmentSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            EquipmentSupplyTracker.TrackEquipmentSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            EquipmentSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return EquipmentSupplyTracker.TryGetEquipmentInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return EquipmentSupplyTracker.TryResolveEquipmentItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return EquipmentSupplyTracker.TryNormalizeEquipmentItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return EquipmentSupplyTracker.GetEquipmentDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static equipment trait service.</summary>
    internal sealed class EquipmentCategoryTraitServiceAdapter : ICategoryTraitService<EquipmentEconomicTrait>
    {
        public EquipmentEconomicTrait GetTraits(Item? item)
        {
            return EquipmentTraitService.GetTraits(item);
        }

        public EquipmentEconomicTrait GetTraits(string? itemId)
        {
            return EquipmentTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, EquipmentEconomicTrait trait)
        {
            return EquipmentTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(EquipmentEconomicTrait traits)
        {
            return EquipmentTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return EquipmentTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            EquipmentTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static equipment trait economy rules.</summary>
    internal sealed class EquipmentCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return EquipmentTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static equipment category registry.</summary>
    internal sealed class EquipmentEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableEquipmentEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableEquipmentEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return EquipmentEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableEquipmentEconomyCategoryDefinition definition)
        {
            return EquipmentEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static equipment actor simulation service.</summary>
    internal sealed class EquipmentCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<EquipmentMarketSimulationActorState>
    {
        public List<EquipmentMarketSimulationActorState> CreateDefaultActorStates()
        {
            return EquipmentMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<EquipmentMarketSimulationActorState> NormalizeLoadedActors(
            List<EquipmentMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return EquipmentMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
