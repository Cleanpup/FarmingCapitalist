using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for mining item traits.
    /// </summary>
    internal static class MineralTraitService
    {
        internal static IMonitor? Monitor;

        public static MineralEconomicTrait GetTraits(Item? item)
        {
            if (!MineralEconomyItemRules.TryGetMineralEconomyObject(item, out SObject miningObject))
                return MineralEconomicTrait.None;

            ObjectData? miningData = null;
            if (Context.IsWorldReady
                && MineralEconomyItemRules.TryNormalizeMineralItemId(miningObject.ItemId, out string miningItemId)
                && Game1.objectData.TryGetValue(miningItemId, out ObjectData? resolvedData))
            {
                miningData = resolvedData;
            }

            return GetTraitsForItem(miningObject, miningData);
        }

        public static MineralEconomicTrait GetTraits(string? mineralItemId)
        {
            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return MineralEconomicTrait.None;

            if (!MineralEconomyItemRules.TryCreateMineralObject(normalizedMineralItemId, out SObject? miningObject) || miningObject is null)
                return MineralEconomicTrait.None;

            ObjectData? miningData = null;
            if (Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedMineralItemId, out ObjectData? resolvedData))
                miningData = resolvedData;

            return GetTraitsForItem(miningObject, miningData);
        }

        public static bool HasTrait(Item? item, MineralEconomicTrait trait)
        {
            if (trait == MineralEconomicTrait.None)
                return false;

            MineralEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool TryGetMineralData(Item? item, out string mineralItemId, out ObjectData? mineralData)
        {
            mineralItemId = string.Empty;
            mineralData = null;

            if (!MineralEconomyItemRules.TryGetMineralEconomyObject(item, out SObject mineralObject))
                return false;

            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralObject.ItemId, out mineralItemId))
                return false;

            return Context.IsWorldReady && Game1.objectData.TryGetValue(mineralItemId, out mineralData);
        }

        public static bool TryGetMineralData(string? mineralItemId, out string normalizedMineralItemId, out ObjectData? mineralData)
        {
            normalizedMineralItemId = string.Empty;
            mineralData = null;

            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out normalizedMineralItemId))
                return false;

            if (!MineralEconomyItemRules.TryCreateMineralObject(normalizedMineralItemId, out _))
                return false;

            return Context.IsWorldReady && Game1.objectData.TryGetValue(normalizedMineralItemId, out mineralData);
        }

        public static string FormatTraits(MineralEconomicTrait traits)
        {
            if (traits == MineralEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (MineralEconomicTrait trait in Enum.GetValues<MineralEconomicTrait>())
            {
                if (trait == MineralEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Mining traits: <null item> -> None";

            MineralEconomicTrait traits = GetTraits(item);
            return $"Mining traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static MineralEconomicTrait GetTraitsForItem(SObject miningObject, ObjectData? miningData)
        {
            _ = miningData;

            MineralEconomicTrait traits = MineralEconomicTrait.None;

            if (ItemCategoryRules.IsStone(miningObject))
                traits |= MineralEconomicTrait.Stone;

            if (ItemCategoryRules.IsCoal(miningObject))
                traits |= MineralEconomicTrait.Coal;

            if (ItemCategoryRules.IsOre(miningObject))
                traits |= MineralEconomicTrait.Ore;

            if (ItemCategoryRules.IsBar(miningObject))
                traits |= MineralEconomicTrait.Bar;

            if (ItemCategoryRules.IsMineral(miningObject))
                traits |= MineralEconomicTrait.Mineral;

            if (ItemCategoryRules.IsGem(miningObject))
                traits |= MineralEconomicTrait.Gem;

            if (ItemCategoryRules.IsGeode(miningObject))
                traits |= MineralEconomicTrait.Geode;

            return traits;
        }
    }
}
