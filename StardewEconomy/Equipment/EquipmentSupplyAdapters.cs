using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static equipment supply data service.</summary>
    internal sealed class EquipmentSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => EquipmentSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            EquipmentSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            EquipmentSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            EquipmentSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return EquipmentSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return EquipmentSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return EquipmentSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return EquipmentSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static equipment supply modifier service.</summary>
    internal sealed class EquipmentSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => EquipmentSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => EquipmentSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => EquipmentSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => EquipmentSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return EquipmentSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            EquipmentSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return EquipmentSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return EquipmentSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return EquipmentSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return EquipmentSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
