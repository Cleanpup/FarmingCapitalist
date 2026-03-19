using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static crafting-extra supply data service.</summary>
    internal sealed class CraftingExtraSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => CraftingExtraSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            CraftingExtraSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            CraftingExtraSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            CraftingExtraSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return CraftingExtraSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return CraftingExtraSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return CraftingExtraSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return CraftingExtraSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static crafting-extra supply modifier service.</summary>
    internal sealed class CraftingExtraSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => CraftingExtraSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => CraftingExtraSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => CraftingExtraSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => CraftingExtraSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return CraftingExtraSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            CraftingExtraSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return CraftingExtraSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return CraftingExtraSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return CraftingExtraSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return CraftingExtraSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
