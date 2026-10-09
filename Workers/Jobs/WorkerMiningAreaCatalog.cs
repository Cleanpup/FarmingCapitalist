using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal static class WorkerMiningAreaCatalog
{
    public static string? AccessReason(string area)
    {
        if (!Context.IsWorldReady || !WorkerMiningPolicy.IsValid(area))
            return "That mining area is unavailable";
        if (area == WorkerMiningPolicy.Quarry
            && !Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccCraftsRoom"))
            return "Repair the quarry bridge before sending a Miner there";
        return Game1.getLocationFromName(WorkerMiningPolicy.GetLocationName(area)) is { IsOutdoors: true }
            ? null : "That mining location is unavailable";
    }
}
