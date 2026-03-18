using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>Thin adapter around the existing static monster-loot supply data service.</summary>
    internal sealed class MonsterLootSupplyDataServiceAdapter : ICategoryDataService
    {
        public float NeutralSupplyScore => MonsterLootSupplyDataService.NeutralSupplyScore;

        public void LoadOrCreateForCurrentSave()
        {
            MonsterLootSupplyDataService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            MonsterLootSupplyDataService.ClearActiveData();
        }

        public void ResetTrackedSupply()
        {
            MonsterLootSupplyDataService.ResetTrackedSupply();
        }

        public IReadOnlyDictionary<string, float> GetSnapshot()
        {
            return MonsterLootSupplyDataService.GetSnapshot();
        }

        public bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            return MonsterLootSupplyDataService.ReplaceTrackedSupplyScores(supplyScores);
        }

        public float GetSupplyScore(string? itemId)
        {
            return MonsterLootSupplyDataService.GetSupplyScore(itemId);
        }

        public float AddSupply(string itemId, float amount, string displayName, string source)
        {
            return MonsterLootSupplyDataService.AddSupply(itemId, amount, displayName, source);
        }
    }

    /// <summary>Thin adapter around the existing static monster-loot supply modifier service.</summary>
    internal sealed class MonsterLootSupplyModifierServiceAdapter : ICategoryModifierService
    {
        public bool ApplyToLiveSellPricing => MonsterLootSupplyModifierService.ApplyToLiveSellPricing;
        public bool HasDebugSellModifierOverride => MonsterLootSupplyModifierService.HasDebugSellModifierOverride;
        public float MinimumAllowedSellModifier => MonsterLootSupplyModifierService.MinimumAllowedSellModifier;
        public float MaximumAllowedSellModifier => MonsterLootSupplyModifierService.MaximumAllowedSellModifier;

        public bool TrySetDebugSellModifierOverride(float modifier, out string error)
        {
            return MonsterLootSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error);
        }

        public void ClearDebugSellModifierOverride()
        {
            MonsterLootSupplyModifierService.ClearDebugSellModifierOverride();
        }

        public bool TryGetDebugSellModifierOverride(out float modifier)
        {
            return MonsterLootSupplyModifierService.TryGetDebugSellModifierOverride(out modifier);
        }

        public float GetSellModifier(Item? item)
        {
            return MonsterLootSupplyModifierService.GetSellModifier(item);
        }

        public float GetSellModifier(string? itemId, string? displayName = null)
        {
            return MonsterLootSupplyModifierService.GetSellModifier(itemId, displayName);
        }

        public string GetDebugSummary(string itemId, string? displayName = null)
        {
            return MonsterLootSupplyModifierService.GetDebugSummary(itemId, displayName);
        }
    }
}
