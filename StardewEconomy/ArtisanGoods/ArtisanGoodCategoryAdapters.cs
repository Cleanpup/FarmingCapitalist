using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static artisan-good supply tracker.</summary>
    internal sealed class ArtisanGoodCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            ArtisanGoodSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            ArtisanGoodSupplyTracker.TrackArtisanGoodSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            ArtisanGoodSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return ArtisanGoodSupplyTracker.TryGetArtisanGoodInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return ArtisanGoodSupplyTracker.TryResolveArtisanGoodItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return ArtisanGoodSupplyTracker.TryNormalizeArtisanGoodItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static artisan-good trait service.</summary>
    internal sealed class ArtisanGoodCategoryTraitServiceAdapter : ICategoryTraitService<ArtisanGoodEconomicTrait>
    {
        public ArtisanGoodEconomicTrait GetTraits(Item? item)
        {
            return ArtisanGoodTraitService.GetTraits(item);
        }

        public ArtisanGoodEconomicTrait GetTraits(string? itemId)
        {
            return ArtisanGoodTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, ArtisanGoodEconomicTrait trait)
        {
            return ArtisanGoodTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(ArtisanGoodEconomicTrait traits)
        {
            return ArtisanGoodTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return ArtisanGoodTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            ArtisanGoodTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static artisan-good trait economy rules.</summary>
    internal sealed class ArtisanGoodCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return ArtisanGoodTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static artisan-good category registry.</summary>
    internal sealed class ArtisanGoodEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableArtisanGoodEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableArtisanGoodEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return ArtisanGoodEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableArtisanGoodEconomyCategoryDefinition definition)
        {
            return ArtisanGoodEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static artisan-good actor simulation service.</summary>
    internal sealed class ArtisanGoodCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<ArtisanGoodMarketSimulationActorState>
    {
        public List<ArtisanGoodMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ArtisanGoodMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<ArtisanGoodMarketSimulationActorState> NormalizeLoadedActors(
            List<ArtisanGoodMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return ArtisanGoodMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
