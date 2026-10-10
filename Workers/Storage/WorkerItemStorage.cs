using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace HireSkilledHelpers.Workers;

internal static class WorkerItemStorage
{
    internal readonly record struct StoreResult(bool FullyStoredInChest, bool ChestDeposit, bool ShippedRemainder);

    public static bool Store(Item item, WorkerHarvestDestination? destination, IMonitor monitor)
    {
        Chest? chest = null;
        if (destination is not null)
            WorkerChestCatalog.TryGetChest(destination, out chest);

        StoreResult result = Store(item, chest, Game1.getFarm(), ex =>
            monitor.Log($"Worker could not add items to the selected chest: {ex.Message}", LogLevel.Warn));
        if (result.ShippedRemainder && destination is not null)
            monitor.Log("Worker storage was unavailable, locked, or full; remaining items went to the shipping bin.", LogLevel.Warn);
        return result.FullyStoredInChest;
    }

    internal static StoreResult Store(Item item, Chest? chest, Farm farm, System.Action<System.Exception> onChestError)
    {
        int originalStack = item.Stack;
        Item? remainder = item;
        bool chestDeposit = false;
        if (chest is not null && !chest.GetMutex().IsLocked())
        {
            try
            {
                remainder = chest.addItem(item);
                chestDeposit = remainder is null || remainder.Stack < originalStack;
            }
            catch (System.Exception ex)
            {
                onChestError(ex);
                remainder = item;
            }
        }

        if (remainder is not null)
            farm.getShippingBin(Game1.MasterPlayer).Add(remainder);

        return new StoreResult(remainder is null, chestDeposit, remainder is not null);
    }
}
