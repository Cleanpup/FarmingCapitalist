using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace FarmingCapitalist.Workers;

internal static class WorkerItemStorage
{
    public static bool Store(Item item, WorkerHarvestDestination? destination, IMonitor monitor)
    {
        Item? remainder = item;
        if (destination is not null && WorkerChestCatalog.TryGetChest(destination, out Chest? chest) && chest is not null
            && !chest.GetMutex().IsLocked())
        {
            try
            {
                remainder = chest.addItem(item);
            }
            catch (System.Exception ex)
            {
                monitor.Log($"Worker could not add items to the selected chest: {ex.Message}", LogLevel.Warn);
                remainder = item;
            }
        }

        if (remainder is not null)
        {
            Game1.getFarm().getShippingBin(Game1.MasterPlayer).Add(remainder);
            if (destination is not null)
                monitor.Log("Worker storage was unavailable, locked, or full; remaining items went to the shipping bin.", LogLevel.Warn);
        }

        return remainder is null;
    }
}
