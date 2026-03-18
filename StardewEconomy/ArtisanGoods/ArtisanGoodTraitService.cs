using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping artisan-good traits.
    /// </summary>
    internal static class ArtisanGoodTraitService
    {
        internal static IMonitor? Monitor;

        public static ArtisanGoodEconomicTrait GetTraits(Item? item)
        {
            if (!ArtisanGoodEconomyItemRules.TryGetArtisanGoodObject(item, out SObject artisanGoodObject))
                return ArtisanGoodEconomicTrait.None;

            return GetTraitsForItem(artisanGoodObject);
        }

        public static ArtisanGoodEconomicTrait GetTraits(string? artisanGoodItemId)
        {
            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return ArtisanGoodEconomicTrait.None;

            if (!ArtisanGoodEconomyItemRules.TryCreateArtisanGoodObject(normalizedArtisanGoodItemId, out SObject? artisanGoodObject)
                || artisanGoodObject is null)
            {
                return ArtisanGoodEconomicTrait.None;
            }

            return GetTraitsForItem(artisanGoodObject);
        }

        public static bool HasTrait(Item? item, ArtisanGoodEconomicTrait trait)
        {
            if (trait == ArtisanGoodEconomicTrait.None)
                return false;

            ArtisanGoodEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? artisanGoodItemId, ArtisanGoodEconomicTrait trait)
        {
            if (trait == ArtisanGoodEconomicTrait.None)
                return false;

            ArtisanGoodEconomicTrait traits = GetTraits(artisanGoodItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(ArtisanGoodEconomicTrait traits)
        {
            if (traits == ArtisanGoodEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (ArtisanGoodEconomicTrait trait in Enum.GetValues<ArtisanGoodEconomicTrait>())
            {
                if (trait == ArtisanGoodEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Artisan-good traits: <null item> -> None";

            ArtisanGoodEconomicTrait traits = GetTraits(item);
            return $"Artisan-good traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static ArtisanGoodEconomicTrait GetTraitsForItem(SObject artisanGoodObject)
        {
            ArtisanGoodEconomicTrait traits = ArtisanGoodEconomicTrait.None;

            if (ArtisanGoodEconomyItemRules.IsAlcoholBeverage(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.AlcoholBeverage;

            if (ArtisanGoodEconomyItemRules.IsPreserve(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.Preserve;

            if (ArtisanGoodEconomyItemRules.IsDairyArtisanGood(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.DairyArtisanGood;

            if (ArtisanGoodEconomyItemRules.IsClothLoomProduct(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.ClothLoomProduct;

            if (ArtisanGoodEconomyItemRules.IsOilProduct(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.OilProduct;

            if (ArtisanGoodEconomyItemRules.IsSpecialtyProcessedGood(artisanGoodObject))
                traits |= ArtisanGoodEconomicTrait.SpecialtyProcessedGood;

            return traits;
        }
    }
}
