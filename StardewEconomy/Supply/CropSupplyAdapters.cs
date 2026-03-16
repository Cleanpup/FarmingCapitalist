using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static crop supply data service.</summary>
    internal sealed class CropSupplyDataServiceAdapter : ISupplyDataService
    {
        public float NeutralSupplyScore => CropSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            CropSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            CropSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            CropSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return CropSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return CropSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return CropSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return CropSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static crop supply modifier service.</summary>
    internal sealed class CropSupplyModifierServiceAdapter : ISupplyModifierService
    {
        public bool ApplyToLiveSellPricing => CropSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => CropSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => CropSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => CropSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return CropSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            CropSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return CropSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return CropSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return CropSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return CropSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
