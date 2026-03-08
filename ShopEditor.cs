using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.GameData.Shops;

namespace FarmingCapitalist;

public class ShopEditor
{
    private readonly IMonitor _monitor;
    private readonly ShopConfigRoot _shopConfigRoot;

    public ShopEditor(IModHelper helper, IMonitor monitor)
    {
        _monitor = monitor;
        _shopConfigRoot = helper.Data.ReadJsonFile<ShopConfigRoot>("assets/shops.json") ?? new ShopConfigRoot();
        ShopPriceRuntimeService.Monitor = monitor;

        _monitor.Log($"Loaded {_shopConfigRoot.Shops.Count} configured shops from assets/shops.json.", LogLevel.Info);
    }

    public void Apply(ShopMenu shop)
    {
        if (shop.currency != 0)
        {
            _monitor.Log($"Skipping shop {shop.ShopId} because currency {shop.currency} is not gold.", LogLevel.Trace);
            return;
        }

        if (!TryGetShopConfig(shop.ShopId, out ShopConfig config))
            return;

        if (!config.Enabled)
        {
            _monitor.Log($"Skipping disabled shop config: {shop.ShopId}", LogLevel.Trace);
            return;
        }

        string? shopkeeperName = GetShopkeeperName(shop);
        EconomyContext economyContext = EconomyContextBuilder.Build(shopkeeperName, _monitor);

        RemoveConfiguredItems(shop, config.RemoveItems);// shop items changes here
        AddConfiguredItems(shop, config.AddItems);  // and here
        ApplyEconomyPricing(shop, config.ShopPriceMultiplier, economyContext); // item prices changes here
    }

    private bool TryGetShopConfig(string? shopId, out ShopConfig config)
    {
        config = new ShopConfig();
        if (string.IsNullOrWhiteSpace(shopId))
            return false;

        if (_shopConfigRoot.Shops.TryGetValue(shopId, out ShopConfig? exactConfig) && exactConfig is not null)
        {
            config = exactConfig;
            return true;
        }

        foreach (var pair in _shopConfigRoot.Shops)
        {
            if (string.Equals(pair.Key, shopId, StringComparison.OrdinalIgnoreCase) && pair.Value is not null)
            {
                config = pair.Value;
                return true;
            }
        }

        _monitor.Log($"No shop config found for {shopId}.", LogLevel.Trace);
        _monitor.Log($"Discovered shop: {shopId}", LogLevel.Trace);
        return false;
    }

    private void RemoveConfiguredItems(ShopMenu shop, List<string> removeItems)
    {
        if (removeItems.Count == 0)
            return;

        HashSet<string> removeSet = removeItems
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (removeSet.Count == 0)
            return;

        int removedForSale = shop.forSale.RemoveAll(salable => ItemMatchesAnyId(salable, removeSet));

        List<ISalable> keysToRemove = shop.itemPriceAndStock.Keys
            .Where(salable => ItemMatchesAnyId(salable, removeSet))
            .ToList();

        foreach (ISalable key in keysToRemove)
            shop.itemPriceAndStock.Remove(key);

        if (removedForSale > 0 || keysToRemove.Count > 0)
        {
            _monitor.Log(
                $"Removed {removedForSale} forSale entries and {keysToRemove.Count} stock entries from shop {shop.ShopId}.",
                LogLevel.Info
            );
        }
    }

    private void AddConfiguredItems(ShopMenu shop, List<ShopItemConfig> addItems)
    {
        foreach (ShopItemConfig addItem in addItems)
        {
            if (string.IsNullOrWhiteSpace(addItem.ItemId))
                continue;

            if (!IsSeasonValid(addItem.Seasons))
            {
                _monitor.Log(
                    $"Skipped seasonal item {addItem.ItemId} for {shop.ShopId}; current season is {Game1.currentSeason}.",
                    LogLevel.Trace
                );
                continue;
            }

            ISalable saleItem = EnsureSingleForSaleEntry(shop, addItem.ItemId);

            List<ISalable> matchingStockKeys = shop.itemPriceAndStock.Keys
                .Where(key => ItemMatchesId(key, addItem.ItemId))
                .ToList();

            foreach (ISalable key in matchingStockKeys)
                shop.itemPriceAndStock.Remove(key);

            int stock = addItem.Stock == -1 ? int.MaxValue : Math.Max(0, addItem.Stock);
            int price = Math.Max(1, addItem.BasePrice);

            shop.itemPriceAndStock[saleItem] = new ItemStockInformation(
                price: price,
                stock: stock,
                tradeItem: null,
                tradeItemCount: null,
                stockMode: LimitedStockMode.None,
                syncedKey: string.IsNullOrWhiteSpace(addItem.SyncedKey) ? null : addItem.SyncedKey
            );

            _monitor.Log($"Added/updated {GetItemName(saleItem)} ({addItem.ItemId}) in shop {shop.ShopId}.", LogLevel.Info);
        }
    }

    private bool IsSeasonValid(List<string>? seasons)
    {
        if (seasons is null || seasons.Count == 0)
            return true;

        string currentSeason = Game1.currentSeason;
        return seasons.Any(season => string.Equals(season, currentSeason, StringComparison.OrdinalIgnoreCase));
    }

    private ISalable EnsureSingleForSaleEntry(ShopMenu shop, string itemId)
    {
        List<ISalable> matches = shop.forSale
            .Where(item => ItemMatchesId(item, itemId))
            .ToList();

        if (matches.Count == 0)
        {
            ISalable newItem = new StardewValley.Object(itemId, 1);
            shop.forSale.Add(newItem);
            return newItem;
        }

        ISalable keep = matches[0];
        if (matches.Count > 1)
        {
            foreach (ISalable duplicate in matches.Skip(1))
                shop.forSale.Remove(duplicate);

            _monitor.Log($"Removed duplicate forSale entries for item {itemId} in shop {shop.ShopId}.", LogLevel.Trace);
        }

        return keep;
    }

    private void ApplyEconomyPricing(ShopMenu shop, float shopPriceMultiplier, EconomyContext context)
    {
        ShopPriceRuntimeService.AttachShop(shop, shopPriceMultiplier, context);
        _monitor.Log($"Adjusted buy prices for shop {shop.ShopId} (x{shopPriceMultiplier}).", LogLevel.Info);
    }

    private string? GetShopkeeperName(ShopMenu shop)
    {
        ShopData? data = shop.ShopData;

        if (data?.Owners is null || data.Owners.Count == 0)
            return null;

        foreach (ShopOwnerData owner in data.Owners)
        {
            if (!GameStateQuery.CheckConditions(owner.Condition))
                continue;

            if (owner.Type == ShopOwnerType.NamedNpc && !string.IsNullOrWhiteSpace(owner.Name))
                return owner.Name;
        }

        return null;
    }

    private bool ItemMatchesAnyId(ISalable salable, HashSet<string> ids)
    {
        string? itemId = TryGetItemId(salable);
        return itemId is not null && ids.Contains(itemId);
    }

    private bool ItemMatchesId(ISalable salable, string itemId)
    {
        string? salableId = TryGetItemId(salable);
        return salableId is not null && string.Equals(salableId, itemId, StringComparison.OrdinalIgnoreCase);
    }

    private string? TryGetItemId(ISalable salable)
    {
        if (salable is Item item && !string.IsNullOrWhiteSpace(item.ItemId))
            return item.ItemId;

        return null;
    }

    private string GetItemName(ISalable s)
    {
        if (s is StardewValley.Object obj) return obj.Name;
        if (s is StardewValley.Item it) return it.Name;
        return s?.ToString() ?? "<unknown>";
    }
}
