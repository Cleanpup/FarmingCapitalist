using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static animal product supply data service.</summary>
    internal sealed class AnimalProductSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => AnimalProductSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            AnimalProductSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            AnimalProductSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            AnimalProductSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return AnimalProductSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return AnimalProductSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return AnimalProductSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return AnimalProductSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static animal product supply modifier service.</summary>
    internal sealed class AnimalProductSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => AnimalProductSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => AnimalProductSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => AnimalProductSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => AnimalProductSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return AnimalProductSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            AnimalProductSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return AnimalProductSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return AnimalProductSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return AnimalProductSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return AnimalProductSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
