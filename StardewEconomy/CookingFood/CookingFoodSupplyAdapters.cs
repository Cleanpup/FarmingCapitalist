using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static cooking-food supply data service.</summary>
    internal sealed class CookingFoodSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => CookingFoodSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            CookingFoodSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            CookingFoodSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            CookingFoodSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return CookingFoodSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return CookingFoodSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return CookingFoodSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return CookingFoodSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static cooking-food supply modifier service.</summary>
    internal sealed class CookingFoodSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => CookingFoodSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => CookingFoodSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => CookingFoodSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => CookingFoodSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return CookingFoodSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            CookingFoodSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return CookingFoodSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return CookingFoodSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return CookingFoodSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return CookingFoodSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
