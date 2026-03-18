using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static artisan-good supply data service.</summary>
    internal sealed class ArtisanGoodSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => ArtisanGoodSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            ArtisanGoodSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            ArtisanGoodSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            ArtisanGoodSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return ArtisanGoodSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return ArtisanGoodSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return ArtisanGoodSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return ArtisanGoodSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static artisan-good supply modifier service.</summary>
    internal sealed class ArtisanGoodSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => ArtisanGoodSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => ArtisanGoodSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => ArtisanGoodSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return ArtisanGoodSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            ArtisanGoodSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return ArtisanGoodSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return ArtisanGoodSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return ArtisanGoodSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return ArtisanGoodSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
