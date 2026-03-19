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
        public List<string> AnimalProductBonusCategories { get; set; } = new();
        public List<string> AnimalProductNerfCategories { get; set; } = new();
        public List<string> ForageableBonusCategories { get; set; } = new();
        public List<string> ForageableNerfCategories { get; set; } = new();
        public List<string> PlantExtraBonusCategories { get; set; } = new();
        public List<string> PlantExtraNerfCategories { get; set; } = new();
        public List<string> CraftingExtraBonusCategories { get; set; } = new();
        public List<string> CraftingExtraNerfCategories { get; set; } = new();
        public List<string> ArtisanGoodBonusCategories { get; set; } = new();
        public List<string> ArtisanGoodNerfCategories { get; set; } = new();
        public List<string> CookingFoodBonusCategories { get; set; } = new();
        public List<string> CookingFoodNerfCategories { get; set; } = new();
        public List<string> MonsterLootBonusCategories { get; set; } = new();
        public List<string> MonsterLootNerfCategories { get; set; } = new();
        public List<string> EquipmentBonusCategories { get; set; } = new();
        public List<string> EquipmentNerfCategories { get; set; } = new();

        public Dictionary<string, float> BuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> SellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> FishSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> MineralSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> AnimalProductBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> AnimalProductSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> ForageableBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> ForageableSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> PlantExtraBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> PlantExtraSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> CraftingExtraBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> CraftingExtraSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> ArtisanGoodBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> ArtisanGoodSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> CookingFoodBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> CookingFoodSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> MonsterLootBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> MonsterLootSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> EquipmentBuyMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> EquipmentSellMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
