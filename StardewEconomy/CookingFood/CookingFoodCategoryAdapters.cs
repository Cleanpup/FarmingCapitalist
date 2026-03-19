using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static cooking-food supply tracker.</summary>
    internal sealed class CookingFoodCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            CookingFoodSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            CookingFoodSupplyTracker.TrackCookingFoodSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            CookingFoodSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return CookingFoodSupplyTracker.TryGetCookingFoodInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return CookingFoodSupplyTracker.TryResolveCookingFoodItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return CookingFoodSupplyTracker.TryNormalizeCookingFoodItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return CookingFoodSupplyTracker.GetCookingFoodDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static cooking-food trait service.</summary>
    internal sealed class CookingFoodCategoryTraitServiceAdapter : ICategoryTraitService<CookingFoodEconomicTrait>
    {
        public CookingFoodEconomicTrait GetTraits(Item? item)
        {
            return CookingFoodTraitService.GetTraits(item);
        }

        public CookingFoodEconomicTrait GetTraits(string? itemId)
        {
            return CookingFoodTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, CookingFoodEconomicTrait trait)
        {
            return CookingFoodTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(CookingFoodEconomicTrait traits)
        {
            return CookingFoodTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return CookingFoodTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            CookingFoodTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static cooking-food trait economy rules.</summary>
    internal sealed class CookingFoodCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return CookingFoodTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static cooking-food category registry.</summary>
    internal sealed class CookingFoodEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableCookingFoodEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableCookingFoodEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return CookingFoodEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableCookingFoodEconomyCategoryDefinition definition)
        {
            return CookingFoodEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static cooking-food actor simulation service.</summary>
    internal sealed class CookingFoodCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<CookingFoodMarketSimulationActorState>
    {
        public List<CookingFoodMarketSimulationActorState> CreateDefaultActorStates()
        {
            return CookingFoodMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<CookingFoodMarketSimulationActorState> NormalizeLoadedActors(
            List<CookingFoodMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return CookingFoodMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
