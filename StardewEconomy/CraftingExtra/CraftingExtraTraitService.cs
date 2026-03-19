using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for crafting-extra trait identity.
    /// This category has no user-facing subcategories, so every eligible item maps to the same internal trait.
    /// </summary>
    internal static class CraftingExtraTraitService
    {
        internal static IMonitor? Monitor;

        public static CraftingExtraEconomicTrait GetTraits(Item? item)
        {
            if (!CraftingExtraEconomyItemRules.TryGetCraftingExtraObject(item, out SObject craftingExtraObject))
                return CraftingExtraEconomicTrait.None;

            return GetTraitsForItem(craftingExtraObject);
        }

        public static CraftingExtraEconomicTrait GetTraits(string? craftingExtraItemId)
        {
            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return CraftingExtraEconomicTrait.None;

            if (!CraftingExtraEconomyItemRules.TryCreateCraftingExtraObject(normalizedCraftingExtraItemId, out SObject? craftingExtraObject)
                || craftingExtraObject is null)
            {
                return CraftingExtraEconomicTrait.None;
            }

            return GetTraitsForItem(craftingExtraObject);
        }

        public static bool HasTrait(Item? item, CraftingExtraEconomicTrait trait)
        {
            if (trait == CraftingExtraEconomicTrait.None)
                return false;

            CraftingExtraEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? craftingExtraItemId, CraftingExtraEconomicTrait trait)
        {
            if (trait == CraftingExtraEconomicTrait.None)
                return false;

            CraftingExtraEconomicTrait traits = GetTraits(craftingExtraItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(CraftingExtraEconomicTrait traits)
        {
            if (traits == CraftingExtraEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (CraftingExtraEconomicTrait trait in Enum.GetValues<CraftingExtraEconomicTrait>())
            {
                if (trait == CraftingExtraEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "CraftingExtra traits: <null item> -> None";

            CraftingExtraEconomicTrait traits = GetTraits(item);
            return $"CraftingExtra traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static CraftingExtraEconomicTrait GetTraitsForItem(SObject craftingExtraObject)
        {
            return CraftingExtraEconomyItemRules.IsCraftingExtraEligible(craftingExtraObject)
                ? CraftingExtraEconomicTrait.Material
                : CraftingExtraEconomicTrait.None;
        }
    }
}
