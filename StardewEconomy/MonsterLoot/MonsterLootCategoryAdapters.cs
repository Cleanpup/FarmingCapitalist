using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static monster-loot supply tracker.</summary>
    internal sealed class MonsterLootCategorySupplyTrackerAdapter : ICategorySupplyTracker
    {
        public void TrackSale(Item? item, int quantity, string source)
        {
            MonsterLootSupplyTracker.TrackSale(item, quantity, source);
        }

        public void TrackCategorySale(string itemId, string displayName, int quantity, string source)
        {
            MonsterLootSupplyTracker.TrackMonsterLootSale(itemId, displayName, quantity, source);
        }

        public void TrackItems(IEnumerable<Item> items, string source)
        {
            MonsterLootSupplyTracker.TrackItems(items, source);
        }

        public bool TryGetItemInfo(Item? item, out string itemId, out string displayName)
        {
            return MonsterLootSupplyTracker.TryGetMonsterLootInfo(item, out itemId, out displayName);
        }

        public bool TryResolveItemId(string? rawInput, out string itemId, out string displayName)
        {
            return MonsterLootSupplyTracker.TryResolveMonsterLootItemId(rawInput, out itemId, out displayName);
        }

        public bool TryNormalizeItemId(string? rawItemId, out string normalizedItemId)
        {
            return MonsterLootSupplyTracker.TryNormalizeMonsterLootItemId(rawItemId, out normalizedItemId);
        }

        public string GetDisplayName(string? itemId)
        {
            return MonsterLootSupplyTracker.GetMonsterLootDisplayName(itemId);
        }
    }

    /// <summary>Thin adapter around the existing static monster-loot trait service.</summary>
    internal sealed class MonsterLootCategoryTraitServiceAdapter : ICategoryTraitService<MonsterLootEconomicTrait>
    {
        public MonsterLootEconomicTrait GetTraits(Item? item)
        {
            return MonsterLootTraitService.GetTraits(item);
        }

        public MonsterLootEconomicTrait GetTraits(string? itemId)
        {
            return MonsterLootTraitService.GetTraits(itemId);
        }

        public bool HasTrait(Item? item, MonsterLootEconomicTrait trait)
        {
            return MonsterLootTraitService.HasTrait(item, trait);
        }

        public string FormatTraits(MonsterLootEconomicTrait traits)
        {
            return MonsterLootTraitService.FormatTraits(traits);
        }

        public string GetDebugSummary(Item? item)
        {
            return MonsterLootTraitService.GetDebugSummary(item);
        }

        public void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            MonsterLootTraitService.LogTraits(item, level);
        }
    }

    /// <summary>Thin adapter around the existing static monster-loot trait economy rules.</summary>
    internal sealed class MonsterLootCategoryTraitEconomyRulesAdapter : ICategoryTraitEconomyRules
    {
        public float GetSellTraitModifier(Item item, EconomyContext context)
        {
            return MonsterLootTraitEconomyRules.GetSellTraitModifier(item, context);
        }
    }

    /// <summary>Thin adapter around the existing static monster-loot category registry.</summary>
    internal sealed class MonsterLootEconomyCategoryRegistryAdapter : ICategoryDefinitionRegistry<RandomizableMonsterLootEconomyCategoryDefinition>
    {
        public IReadOnlyList<RandomizableMonsterLootEconomyCategoryDefinition> GetRandomizableCategories()
        {
            return MonsterLootEconomyCategoryRegistry.GetRandomizableCategories();
        }

        public bool TryGetCategory(string? key, out RandomizableMonsterLootEconomyCategoryDefinition definition)
        {
            return MonsterLootEconomyCategoryRegistry.TryGetCategory(key, out definition);
        }
    }

    /// <summary>Thin adapter around the existing static monster-loot actor simulation service.</summary>
    internal sealed class MonsterLootCategoryMarketActorSimulationServiceAdapter : ICategoryMarketActorSimulationService<MonsterLootMarketSimulationActorState>
    {
        public List<MonsterLootMarketSimulationActorState> CreateDefaultActorStates()
        {
            return MonsterLootMarketActorSimulationService.CreateDefaultActorStates();
        }

        public List<MonsterLootMarketSimulationActorState> NormalizeLoadedActors(
            List<MonsterLootMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            return MonsterLootMarketActorSimulationService.NormalizeLoadedActors(loadedActors, out shouldPersist);
        }
    }
}
