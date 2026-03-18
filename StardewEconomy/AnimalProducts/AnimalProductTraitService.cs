using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping animal-product traits.
    /// </summary>
    internal static class AnimalProductTraitService
    {
        internal static IMonitor? Monitor;

        public static AnimalProductEconomicTrait GetTraits(Item? item)
        {
            if (!AnimalProductEconomyItemRules.TryGetAnimalProductObject(item, out SObject animalProductObject))
                return AnimalProductEconomicTrait.None;

            return GetTraitsForItem(animalProductObject);
        }

        public static AnimalProductEconomicTrait GetTraits(string? animalProductItemId)
        {
            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return AnimalProductEconomicTrait.None;

            if (!AnimalProductEconomyItemRules.TryCreateAnimalProductObject(normalizedAnimalProductItemId, out SObject? animalProductObject)
                || animalProductObject is null)
            {
                return AnimalProductEconomicTrait.None;
            }

            return GetTraitsForItem(animalProductObject);
        }

        public static bool HasTrait(Item? item, AnimalProductEconomicTrait trait)
        {
            if (trait == AnimalProductEconomicTrait.None)
                return false;

            AnimalProductEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? animalProductItemId, AnimalProductEconomicTrait trait)
        {
            if (trait == AnimalProductEconomicTrait.None)
                return false;

            AnimalProductEconomicTrait traits = GetTraits(animalProductItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(AnimalProductEconomicTrait traits)
        {
            if (traits == AnimalProductEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (AnimalProductEconomicTrait trait in Enum.GetValues<AnimalProductEconomicTrait>())
            {
                if (trait == AnimalProductEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Animal product traits: <null item> -> None";

            AnimalProductEconomicTrait traits = GetTraits(item);
            return $"Animal product traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        public static bool IsSeasonalTruffle(Item? item) => AnimalProductEconomyItemRules.IsSeasonalTruffle(item);
        public static bool IsSeasonalTruffle(string? animalProductItemId) => AnimalProductEconomyItemRules.IsSeasonalTruffle(animalProductItemId);

        private static AnimalProductEconomicTrait GetTraitsForItem(SObject animalProductObject)
        {
            AnimalProductEconomicTrait traits = AnimalProductEconomicTrait.None;

            if (AnimalProductEconomyItemRules.IsEggProduct(animalProductObject))
                traits |= AnimalProductEconomicTrait.Egg;

            if (AnimalProductEconomyItemRules.IsMilkProduct(animalProductObject))
                traits |= AnimalProductEconomicTrait.Milk;

            if (AnimalProductEconomyItemRules.IsFiberAnimalProduct(animalProductObject))
                traits |= AnimalProductEconomicTrait.FiberAnimalProduct;

            if (AnimalProductEconomyItemRules.IsCoopProduct(animalProductObject))
                traits |= AnimalProductEconomicTrait.CoopProduct;

            if (AnimalProductEconomyItemRules.IsBarnProduct(animalProductObject))
                traits |= AnimalProductEconomicTrait.BarnProduct;

            if (AnimalProductEconomyItemRules.IsSpecialtyAnimalGood(animalProductObject))
                traits |= AnimalProductEconomicTrait.SpecialtyAnimalGood;

            return traits;
        }
    }
}
