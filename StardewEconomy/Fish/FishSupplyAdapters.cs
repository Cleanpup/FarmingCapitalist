using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static fish supply data service.</summary>
    internal sealed class FishSupplyDataServiceAdapter : ISupplyDataService
    {
        public float NeutralSupplyScore => FishSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            FishSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            FishSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            FishSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return FishSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return FishSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return FishSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return FishSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static fish supply modifier service.</summary>
    internal sealed class FishSupplyModifierServiceAdapter : ISupplyModifierService
    {
        public bool ApplyToLiveSellPricing => FishSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => FishSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => FishSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => FishSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return FishSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            FishSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return FishSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return FishSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return FishSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return FishSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
