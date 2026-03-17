using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static mineral supply data service.</summary>
    internal sealed class MineralSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => MineralSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            MineralSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            MineralSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            MineralSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return MineralSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return MineralSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return MineralSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return MineralSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static mineral supply modifier service.</summary>
    internal sealed class MineralSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => MineralSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => MineralSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => MineralSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => MineralSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return MineralSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            MineralSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return MineralSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return MineralSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return MineralSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return MineralSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
