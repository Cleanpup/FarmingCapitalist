using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping monster-loot traits.
    /// </summary>
    internal static class MonsterLootTraitService
    {
        internal static IMonitor? Monitor;

        public static MonsterLootEconomicTrait GetTraits(Item? item)
        {
            if (!MonsterLootEconomyItemRules.TryGetMonsterLootObject(item, out SObject monsterLootObject))
                return MonsterLootEconomicTrait.None;

            return GetTraitsForItem(monsterLootObject);
        }

        public static MonsterLootEconomicTrait GetTraits(string? monsterLootItemId)
        {
            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return MonsterLootEconomicTrait.None;

            if (!MonsterLootEconomyItemRules.TryCreateMonsterLootObject(normalizedMonsterLootItemId, out SObject? monsterLootObject)
                || monsterLootObject is null)
            {
                return MonsterLootEconomicTrait.None;
            }

            return GetTraitsForItem(monsterLootObject);
        }

        public static bool HasTrait(Item? item, MonsterLootEconomicTrait trait)
        {
            if (trait == MonsterLootEconomicTrait.None)
                return false;

            MonsterLootEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? monsterLootItemId, MonsterLootEconomicTrait trait)
        {
            if (trait == MonsterLootEconomicTrait.None)
                return false;

            MonsterLootEconomicTrait traits = GetTraits(monsterLootItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(MonsterLootEconomicTrait traits)
        {
            if (traits == MonsterLootEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (MonsterLootEconomicTrait trait in Enum.GetValues<MonsterLootEconomicTrait>())
            {
                if (trait == MonsterLootEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Monster-loot traits: <null item> -> None";

            MonsterLootEconomicTrait traits = GetTraits(item);
            return $"Monster-loot traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static MonsterLootEconomicTrait GetTraitsForItem(SObject monsterLootObject)
        {
            MonsterLootEconomicTrait traits = MonsterLootEconomicTrait.None;

            if (MonsterLootEconomyItemRules.IsBasicMonsterDrop(monsterLootObject))
                traits |= MonsterLootEconomicTrait.BasicMonsterDrop;

            if (MonsterLootEconomyItemRules.IsSlimeRelatedItem(monsterLootObject))
                traits |= MonsterLootEconomicTrait.SlimeRelatedItem;

            if (MonsterLootEconomyItemRules.IsEssenceMagicalDrop(monsterLootObject))
                traits |= MonsterLootEconomicTrait.EssenceMagicalDrop;

            return traits;
        }
    }
}
