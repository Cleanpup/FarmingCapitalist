using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace HireSkilledHelpers.Workers;

internal static class WorkerMiningAreaCatalog
{
    public static string? AccessReason(string area)
    {
        if (!Context.IsWorldReady || !WorkerMiningPolicy.IsValid(area))
            return "That mining area is unavailable";
        if (area == WorkerMiningPolicy.Quarry
            && !Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccCraftsRoom"))
            return "Repair the quarry bridge before sending a Miner there";
        if (WorkerMiningPolicy.IsDungeon(area)
            && WorkerExplorationManager.AccessReason(area) is string reason)
            return reason.Replace("exploring", "mining", StringComparison.Ordinal);
        return Game1.getLocationFromName(WorkerMiningPolicy.GetLocationName(area)) is not null
            ? null : "That mining location is unavailable";
    }

    public static bool HasMineExit(GameLocation location)
    {
        if (location is not MineShaft mine) return false;
        if (mine.ladderHasSpawned) return true;
        // A shaft isn't reflected in ladderHasSpawned; include authored/elevator-floor
        // ladders and player-placed staircases as well. Read the map; never create one.
        var buildings = location.Map.GetLayer("Buildings");
        if (buildings is null) return false;
        for (int x = 0; x < buildings.LayerWidth; x++)
        for (int y = 0; y < buildings.LayerHeight; y++)
            if (buildings.Tiles[x, y]?.TileIndex is 173 or 174) return true;
        return false;
    }
}
