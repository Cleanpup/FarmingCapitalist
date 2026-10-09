using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal static class WorkerExplorationLootStorage
{
    public static bool TryDeliver(WorkerExplorationProgress progress, WorkerHarvestDestination? destination,
        IMonitor monitor, Action persist, out string error)
    {
        error = string.Empty;
        while (progress.PendingLoot.Count > 0)
        {
            WorkerExplorationLoot drop = progress.PendingLoot[0];
            if (!WorkerExplorationPolicy.IsAllowedPendingLoot(drop))
            {
                // The revised order explicitly forbids old simulated mining
                // rewards. Remove only queued items, never existing player stock.
                monitor.Log($"Cancelled obsolete simulated exploration reward {drop.ItemId} x{drop.Stack}; it is outside the monster-only loot policy.", LogLevel.Info);
                progress.PendingLoot.RemoveAt(0);
                persist();
                continue;
            }
            Item? item = null;
            try
            {
                item = ItemRegistry.Create(drop.ItemId, drop.Stack);
                WorkerItemStorage.Store(item, destination, monitor);
                progress.PendingLoot.RemoveAt(0);
                persist();
            }
            catch (Exception ex)
            {
                // Preserve only the remainder if a chest accepted part of the stack
                // before a later error. Retrying delivery never grants run XP again.
                if (item is not null) drop.Stack = Math.Max(0, item.Stack);
                if (drop.Stack == 0) progress.PendingLoot.RemoveAt(0);
                persist();
                error = ex.Message;
                return false;
            }
        }
        return true;
    }
}
