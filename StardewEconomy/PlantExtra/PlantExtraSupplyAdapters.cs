using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static plantExtra supply data service.</summary>
    internal sealed class PlantExtraSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => PlantExtraSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            PlantExtraSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            PlantExtraSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            PlantExtraSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return PlantExtraSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return PlantExtraSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return PlantExtraSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return PlantExtraSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static plantExtra supply modifier service.</summary>
    internal sealed class PlantExtraSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => PlantExtraSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => PlantExtraSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => PlantExtraSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => PlantExtraSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return PlantExtraSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            PlantExtraSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return PlantExtraSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return PlantExtraSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return PlantExtraSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return PlantExtraSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
