using System.Linq;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;

namespace FarmingCapitalist
{
    /// <summary>
    /// Equipment-owned eligibility and normalization helpers.
    /// Equipment IDs are stored as qualified IDs because the category spans
    /// multiple item types: weapons, rings, and boots.
    /// </summary>
    internal static class EquipmentEconomyItemRules
    {
        private static readonly IReadOnlyList<string> CandidateTypeDefinitionIds = new[]
        {
            ItemRegistry.type_weapon,
            ItemRegistry.type_object,
            ItemRegistry.type_boots
        };

        public static bool IsEquipmentEligible(Item? item)
        {
            return TryGetEquipmentItem(item, out _);
        }

        public static bool TryGetEquipmentItem(Item? item, out Item equipmentItem)
        {
            equipmentItem = null!;
            if (!IsSellableEquipment(item))
                return false;

            if (!IsWeapon(item) && !IsRing(item) && !IsBoots(item))
                return false;

            equipmentItem = item!;
            return true;
        }

        public static bool TryNormalizeEquipmentItemId(string? rawEquipmentItemId, out string normalizedEquipmentItemId)
        {
            normalizedEquipmentItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawEquipmentItemId))
                return false;

            string candidate = rawEquipmentItemId.Trim();
            if (ItemRegistry.IsQualifiedItemId(candidate))
            {
                if (!TryCreateEquipmentItemDirect(candidate, out Item? directItem) || directItem is null)
                    return false;

                normalizedEquipmentItemId = directItem.QualifiedItemId;
                return true;
            }

            HashSet<string> matches = new(StringComparer.OrdinalIgnoreCase);
            foreach (string typeDefinitionId in CandidateTypeDefinitionIds)
            {
                string qualifiedCandidate = ItemRegistry.ManuallyQualifyItemId(candidate, typeDefinitionId);
                if (!TryCreateEquipmentItemDirect(qualifiedCandidate, out Item? typedItem) || typedItem is null)
                    continue;

                matches.Add(typedItem.QualifiedItemId);
            }

            if (matches.Count != 1)
                return false;

            normalizedEquipmentItemId = matches.First();
            return true;
        }

        public static bool TryCreateEquipmentItem(string rawItemId, out Item? equipmentItem)
        {
            equipmentItem = null;
            if (!TryNormalizeEquipmentItemId(rawItemId, out string normalizedEquipmentItemId))
                return false;

            return TryCreateEquipmentItemDirect(normalizedEquipmentItemId, out equipmentItem);
        }

        public static bool IsWeapon(Item? item)
        {
            if (!IsSellableEquipment(item))
                return false;

            if (!string.Equals(item!.TypeDefinitionId, ItemRegistry.type_weapon, StringComparison.OrdinalIgnoreCase))
                return false;

            return item is not MeleeWeapon meleeWeapon || !meleeWeapon.isScythe();
        }

        public static bool IsRing(Item? item)
        {
            return item is Ring && IsSellableEquipment(item);
        }

        public static bool IsBoots(Item? item)
        {
            return item is Boots && IsSellableEquipment(item);
        }

        public static bool IsWearableEquipment(Item? item)
        {
            return (item is Ring || item is Boots) && IsSellableEquipment(item);
        }

        public static bool IsEquipmentItemId(string? equipmentItemId)
        {
            return TryCreateEquipmentItem(equipmentItemId ?? string.Empty, out _);
        }

        private static bool TryCreateEquipmentItemDirect(string qualifiedEquipmentItemId, out Item? equipmentItem)
        {
            equipmentItem = ItemRegistry.Create(qualifiedEquipmentItemId, allowNull: true);
            return equipmentItem is not null && IsEquipmentEligible(equipmentItem);
        }

        private static bool IsSellableEquipment(Item? item)
        {
            return item is not null
                && item.salePrice() > 0;
        }
    }
}
