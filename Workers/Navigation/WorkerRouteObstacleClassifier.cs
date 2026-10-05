using StardewValley;

namespace FarmingCapitalist.Workers;

internal static class WorkerRouteObstacleClassifier
{
    // These vanilla predicates identify one-tile litter. Placed storage, machines,
    // fences, crops, trees, and multi-tile resource clumps are never clearance work.
    public static bool IsSmallLitter(StardewValley.Object? item)
        => item is not null
            && item.GetType() == typeof(StardewValley.Object)
            && (item.IsWeeds() || item.IsTwig() || item.IsBreakableStone());
}
