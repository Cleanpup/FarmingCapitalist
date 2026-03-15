/* totally unimplemeneted- framework for GMCM config


*/

public class ModConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnableVerbosePriceTrace { get; set; } = true;
    public bool ApplySupplyDemandSellModifier { get; set; } = true;

    public BuyPriceConfig BuyPrices { get; set; } = new();
    public FriendshipConfig Friendship { get; set; } = new();
    public BulkBuyConfig BulkBuy { get; set; } = new();
    public MarketConfig Market { get; set; } = new();
    public ClampConfig Clamp { get; set; } = new();

    public Dictionary<string, CategoryConfig> Categories { get; set; } = new(); // keys like "Seeds", "Fish", etc.
    public DebugConfig Debug { get; set; } = new();
}

public class BuyPriceConfig { public bool Enabled { get; set; } = true; }

public class FriendshipConfig
{
    public bool Enabled { get; set; } = true;
    public float MaxDiscountPercent { get; set; } = 0.15f; // 15%
}

public class BulkBuyConfig
{
    public bool Enabled { get; set; } = true;
    public bool ByCategory { get; set; } = true;
    public int StepSize { get; set; } = 100;
    public float IncreasePerStep { get; set; } = 0.10f; // +10% per step
    public float MaxMultiplier { get; set; } = 2.0f;     // cap at 2x
    public bool ResetDaily { get; set; } = true;
    public float DailyDecay { get; set; } = 0.0f;        // if not reset, decay
}

public class MarketConfig
{
    public bool Enabled { get; set; } = false;
    public float Strength { get; set; } = 1.0f;
    public float DailyEquilibriumPull { get; set; } = 0.10f;
    public float Smoothing { get; set; } = 0.50f;
    public float ShockChancePerDay { get; set; } = 0.05f;
    public float ShockStrength { get; set; } = 0.25f;
    public int ShockDurationDays { get; set; } = 3;
}

public class ClampConfig
{
    public float MinMultiplier { get; set; } = 0.50f;
    public float MaxMultiplier { get; set; } = 3.00f;
    public int MinPrice { get; set; } = 1;
}

public class CategoryConfig
{
    public float BaseMultiplier { get; set; } = 1.0f;
    public float BulkBuySensitivity { get; set; } = 1.0f;
    public float MarketSensitivity { get; set; } = 1.0f;
}

public class DebugConfig
{
    public bool VerboseLogs { get; set; } = true;
}
