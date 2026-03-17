using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Save-scoped static market profile used by economy systems.
    /// Stored with SMAPI save data so each save can have its own persistent profile.
    /// </summary>
    public sealed class SaveEconomyProfile
    {
        public string ProfileId { get; set; } = "Randomized";
        public int Seed { get; set; }

        public List<string> BonusCategories { get; set; } = new();
        public List<string> NerfCategories { get; set; } = new();
        public List<string> FishBonusCategories { get; set; } = new();
        public List<string> FishNerfCategories { get; set; } = new();
        public List<string> MineralBonusCategories { get; set; } = new();
        public List<string> MineralNerfCategories { get; set; } = new();

        public Dictionary<string, float> BuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> SellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> FishSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> MineralSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
