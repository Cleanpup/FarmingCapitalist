namespace FarmingCapitalist;

public class ShopConfigRoot
{
    public Dictionary<string, ShopConfig> Shops { get; set; } = new();
}

public class ShopConfig
{
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public float ShopPriceMultiplier { get; set; } = 1.0f;
    public List<ShopItemConfig> AddItems { get; set; } = new();
    public List<string> RemoveItems { get; set; } = new();
}

public class ShopItemConfig
{
    public string ItemId { get; set; } = string.Empty;
    public int BasePrice { get; set; }
    public int Stock { get; set; } = -1;
    public string? SyncedKey { get; set; }
    public List<string>? Seasons { get; set; }
}
