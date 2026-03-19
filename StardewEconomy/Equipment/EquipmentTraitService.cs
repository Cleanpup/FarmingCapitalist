using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for overlapping equipment traits.
    /// </summary>
    internal static class EquipmentTraitService
    {
        internal static IMonitor? Monitor;

        public static EquipmentEconomicTrait GetTraits(Item? item)
        {
            if (!EquipmentEconomyItemRules.TryGetEquipmentItem(item, out Item equipmentItem))
                return EquipmentEconomicTrait.None;

            return GetTraitsForItem(equipmentItem);
        }

        public static EquipmentEconomicTrait GetTraits(string? equipmentItemId)
        {
            if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId))
                return EquipmentEconomicTrait.None;

            if (!EquipmentEconomyItemRules.TryCreateEquipmentItem(normalizedEquipmentItemId, out Item? equipmentItem)
                || equipmentItem is null)
            {
                return EquipmentEconomicTrait.None;
            }

            return GetTraitsForItem(equipmentItem);
        }

        public static bool HasTrait(Item? item, EquipmentEconomicTrait trait)
        {
            if (trait == EquipmentEconomicTrait.None)
                return false;

            EquipmentEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? equipmentItemId, EquipmentEconomicTrait trait)
        {
            if (trait == EquipmentEconomicTrait.None)
                return false;

            EquipmentEconomicTrait traits = GetTraits(equipmentItemId);
            return (traits & trait) == trait;
        }

        public static string FormatTraits(EquipmentEconomicTrait traits)
        {
            if (traits == EquipmentEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (EquipmentEconomicTrait trait in Enum.GetValues<EquipmentEconomicTrait>())
            {
                if (trait == EquipmentEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Equipment traits: <null item> -> None";

            EquipmentEconomicTrait traits = GetTraits(item);
            return $"Equipment traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static EquipmentEconomicTrait GetTraitsForItem(Item equipmentItem)
        {
            EquipmentEconomicTrait traits = EquipmentEconomicTrait.None;

            if (EquipmentEconomyItemRules.IsWeapon(equipmentItem))
                traits |= EquipmentEconomicTrait.Weapon;

            if (EquipmentEconomyItemRules.IsRing(equipmentItem))
                traits |= EquipmentEconomicTrait.Ring;

            if (EquipmentEconomyItemRules.IsBoots(equipmentItem))
                traits |= EquipmentEconomicTrait.Boots;

            if (EquipmentEconomyItemRules.IsWearableEquipment(equipmentItem))
                traits |= EquipmentEconomicTrait.WearableEquipment;

            return traits;
        }
    }
}
