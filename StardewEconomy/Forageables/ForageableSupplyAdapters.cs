using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static forageable supply data service.</summary>
    internal sealed class ForageableSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => ForageableSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            ForageableSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            ForageableSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            ForageableSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return ForageableSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return ForageableSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return ForageableSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return ForageableSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static forageable supply modifier service.</summary>
    internal sealed class ForageableSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => ForageableSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => ForageableSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => ForageableSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => ForageableSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return ForageableSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            ForageableSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return ForageableSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return ForageableSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return ForageableSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return ForageableSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
