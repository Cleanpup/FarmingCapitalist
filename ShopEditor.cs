using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.GameData.Shops;

namespace FarmingCapitalist;

public class ShopEditor
{
    private readonly IMonitor _monitor;

    public ShopEditor(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public void Apply(ShopMenu shop)
    {
        if (shop.currency != 0)
            return;

        AddOrUpdateShopItem(shop, itemId: "745", price: 100, stock: int.MaxValue, syncedKey: "CustomStrawberry");
        ApplyEconomyPricing(shop);
    }

    public void AddOrUpdateShopItem(ShopMenu shop, string itemId, int price, int stock, string syncedKey)
    {
        var item = new StardewValley.Object(itemId, 1);

        bool inForSale = shop.forSale.Any(i => i is StardewValley.Object obj && obj.ItemId == itemId);
        if (!inForSale)
        {
            shop.forSale.Add(item);
            _monitor.Log($"Added {GetItemName(item)} to shop", LogLevel.Info);
        }

        shop.itemPriceAndStock[item] = new ItemStockInformation(
            price: price,
            stock: stock,
            tradeItem: null,
            tradeItemCount: null,
            stockMode: LimitedStockMode.None,
            syncedKey: syncedKey
        );
    }

    public void ApplyEconomyPricing(ShopMenu shop)
    {
        var keys = shop.itemPriceAndStock.Keys.ToList();

        foreach (var item in keys)
        {
            var stock = shop.itemPriceAndStock[item];
            int vanillaPrice = stock.Price;

            int adjusted = EconomyService.AdjustBuyPrice(vanillaPrice);
            adjusted = Math.Max(1, adjusted);

            stock.Price = adjusted;
            shop.itemPriceAndStock[item] = stock;

            _monitor.Log($"Adjusted shop price: {GetItemName(item)} {vanillaPrice} -> {adjusted}", LogLevel.Info);
        }

        _monitor.Log($"Adjusted buy prices for shop {shop.ShopId}", LogLevel.Info);
    }

    private string GetItemName(ISalable s)
    {
        if (s is StardewValley.Object obj) return obj.Name;
        if (s is StardewValley.Item it) return it.Name;
        return s?.ToString() ?? "<unknown>";
    }
}
