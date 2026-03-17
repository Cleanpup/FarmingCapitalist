using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for broad mineral rarity and value traits.
    /// </summary>
    internal static class MineralTraitService
    {
        internal static IMonitor? Monitor;

        private const int CommonPriceMaxInclusive = 75;
        private const int UncommonPriceMaxInclusive = 180;
        private const int LuxuryPriceMinInclusive = 250;

        public static MineralEconomicTrait GetTraits(Item? item)
        {
            if (!TryGetMineralData(item, out _, out ObjectData? mineralData) || mineralData is null)
                return MineralEconomicTrait.None;

            return GetTraitsForData(mineralData.Price);
        }

        public static MineralEconomicTrait GetTraits(string? mineralItemId)
        {
            if (!TryGetMineralData(mineralItemId, out _, out ObjectData? mineralData) || mineralData is null)
                return MineralEconomicTrait.None;

            return GetTraitsForData(mineralData.Price);
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
                return "Mineral traits: <null item> -> None";

            MineralEconomicTrait traits = GetTraits(item);
            return $"Mineral traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static MineralEconomicTrait GetTraitsForData(int price)
        {
            if (price < 0)
                return MineralEconomicTrait.None;

            MineralEconomicTrait traits = GetRarityTrait(price);
            if (price >= LuxuryPriceMinInclusive)
                traits |= MineralEconomicTrait.Luxury;

            return traits;
        }

        private static MineralEconomicTrait GetRarityTrait(int price)
        {
            if (price <= CommonPriceMaxInclusive)
                return MineralEconomicTrait.Common;

            if (price <= UncommonPriceMaxInclusive)
                return MineralEconomicTrait.Uncommon;

            return MineralEconomicTrait.Rare;
        }
    }
}
