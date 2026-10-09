using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace FarmingCapitalist.Workers;

/// <summary>Shared physical dungeon staging, floor following, and safe landing geometry.</summary>
internal sealed class WorkerDungeonTravelManager
{
    private readonly WorkerNavigationManager navigation;
    private readonly IMonitor monitor;

    public WorkerDungeonTravelManager(WorkerNavigationManager navigation, IMonitor monitor)
        => (this.navigation, this.monitor) = (navigation, monitor);

    public static bool IsGeneratedFloor(GameLocation? location) => location is MineShaft or VolcanoDungeon;

    public static bool IsActiveFloor(GameLocation? location) => location switch
    {
        MineShaft mine => MineShaft.activeMines.Contains(mine),
        VolcanoDungeon volcano => VolcanoDungeon.activeLevels.Contains(volcano),
        _ => false,
    };

    public static bool IsMatchingActiveFloor(string area, GameLocation? location)
        => IsActiveFloor(location) && (location is MineShaft mine
            ? WorkerCombatPolicy.IsMatchingMineFloor(area, mine.mineLevel)
            : area == WorkerMiningPolicy.Volcano && location is VolcanoDungeon { level.Value: >= 0 and <= 9 });

    public static GameLocation? Entrance(string area) => Game1.getLocationFromName(area switch
    {
        WorkerCombatAreaCatalog.Mines => "Mine",
        WorkerCombatAreaCatalog.SkullCavern => "SkullCave",
        WorkerMiningPolicy.Volcano => "IslandNorth",
        _ => area,
    });

    public void ExitGeneratedFloorForReturn(NPC worker, string area, Action? beforeTransition = null)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || !IsGeneratedFloor(worker.currentLocation)) return;
        if (Entrance(area) is GameLocation entrance)
            this.TryFollowTransition(worker, entrance, nearFarmer: false, beforeTransition);
    }

    public void FollowHostWarp(NPC worker, string area, Action? beforeTransition = null)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        GameLocation? entrance = Entrance(area);
        if (entrance is null) return;
        bool onFloor = IsGeneratedFloor(worker.currentLocation);
        bool matching = IsMatchingActiveFloor(area, Game1.player.currentLocation);
        if (WorkerCombatPolicy.ShouldFollowOnHostWarp(worker.currentLocation == entrance, onFloor, matching))
            this.TryFollowTransition(worker, Game1.player.currentLocation, nearFarmer: true, beforeTransition);
        else if (onFloor && !matching)
            this.TryFollowTransition(worker, entrance, nearFarmer: false, beforeTransition);
    }

    public GameLocation? ResolveWorkLocation(string area, NPC worker, bool followStaged = false, Action? beforeTransition = null)
    {
        GameLocation? entrance = Entrance(area);
        if (entrance is null) return null;
        bool matching = IsMatchingActiveFloor(area, Game1.player.currentLocation);
        bool onFloor = IsGeneratedFloor(worker.currentLocation);
        if (matching && (onFloor || (followStaged && worker.currentLocation == entrance)))
        {
            if (worker.currentLocation != Game1.player.currentLocation)
                this.TryFollowTransition(worker, Game1.player.currentLocation, nearFarmer: true, beforeTransition);
            // A failed safe landing must retain the previous floor and retry later.
            return worker.currentLocation == Game1.player.currentLocation ? worker.currentLocation : entrance;
        }
        if (onFloor)
            this.TryFollowTransition(worker, entrance, nearFarmer: false, beforeTransition);
        return followStaged || Game1.player.currentLocation == entrance || matching || worker.currentLocation == entrance
            ? entrance : null;
    }

    public bool TryFollowTransition(NPC worker, GameLocation destination, bool nearFarmer, Action? beforeTransition = null)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return false;
        Point landing;
        if (nearFarmer)
        {
            if (!this.TryChooseFloorLanding(destination, worker, out landing))
            {
                this.monitor.Log($"No safe landing with room to move was available for {worker.displayName} anywhere in {destination.NameOrUniqueName}; staying on the previous floor.", LogLevel.Info);
                return false;
            }
            this.monitor.Log($"{worker.displayName} will follow into {destination.NameOrUniqueName} at {landing} "
                + $"({Math.Abs(landing.X - Game1.player.TilePoint.X) + Math.Abs(landing.Y - Game1.player.TilePoint.Y)} tiles from the farmer).", LogLevel.Trace);
        }
        else if (!this.TryChooseLanding(destination, worker, out landing))
            return false;

        this.navigation.StopWorker(worker);
        beforeTransition?.Invoke();
        GameLocation? previous = worker.currentLocation;
        try
        {
            Game1.warpCharacter(worker, destination, landing.ToVector2());
            if (worker.currentLocation == destination && previous != destination)
                previous?.characters.Remove(worker);
            return worker.currentLocation == destination;
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not safely move {worker.displayName} into {destination.NameOrUniqueName}: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    public bool TryChooseLanding(GameLocation location, NPC worker, out Point landing)
    {
        if (location.NameOrUniqueName == "IslandNorth")
            return this.TryFindSafeTileNear(location, worker, new Point(40, 24), 8, out landing);
        bool caveEntrance = location.NameOrUniqueName is "Mine" or "SkullCave";
        if (caveEntrance && Game1.player.currentLocation == location)
            return this.TryChooseConnectedLandingNearFarmer(location, worker, 4, out landing);

        foreach (Warp warp in location.warps)
        {
            if (this.TryFindSafeTileNear(location, worker, new Point(warp.X, warp.Y), caveEntrance ? 4 : 8, out landing))
                return true;
        }
        // Interior cave entrances must be reached from their authored exit area;
        // scanning the whole map can select invisible border tiles as landings.
        if (caveEntrance)
        {
            landing = Point.Zero;
            return false;
        }
        for (int y = 1; y < 80; y++)
        for (int x = 1; x < 100; x++)
        {
            Point candidate = new(x, y);
            if (this.IsSafeLandingTile(location, worker, candidate))
            {
                landing = candidate;
                return true;
            }
        }
        landing = Point.Zero;
        return false;
    }

    private bool TryFindSafeTileNear(GameLocation location, NPC worker, Point anchor, int maxRadius, out Point tile)
    {
        for (int radius = 1; radius <= maxRadius; radius++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                continue;
            Point candidate = new(anchor.X + dx, anchor.Y + dy);
            if (this.IsSafeLandingTile(location, worker, candidate))
            {
                tile = candidate;
                return true;
            }
        }
        tile = Point.Zero;
        return false;
    }

    private bool IsSafeLandingTile(GameLocation location, NPC worker, Point tile)
    {
        // Initial placement must avoid decorative wall/edge tiles. This mine-specific
        // test is too strict for ordinary movement across passable mine overlays.
        bool onMap = this.IsSafeRouteStep(location, tile);
        bool hasGround = onMap && location.hasTileAt(tile.X, tile.Y, "Back");
        if (hasGround && location is MineShaft mine)
            hasGround = mine.isTileOnClearAndSolidGround(tile.ToVector2());
        bool mapPassable = hasGround && location.isTilePassable(tile.ToVector2());
        bool navigationWalkable = mapPassable && this.navigation.IsWalkableWorkTile(location, worker, tile);
        return WorkerCombatTilePolicy.IsSafeTile(onMap, hasGround, mapPassable, navigationWalkable);
    }

    public bool IsSafeMovementTile(GameLocation location, NPC worker, Point tile)
        => this.IsSafeRouteStep(location, tile)
            && this.navigation.IsWalkableWorkTile(location, worker, tile);

    public bool IsSafeRouteStep(GameLocation location, Point tile)
    {
        if (!location.isTileOnMap(tile.ToVector2()))
            return false;
        if (location is VolcanoDungeon volcano && location.isWaterTile(tile.X, tile.Y)
            && !volcano.IsCooledLava(tile.X, tile.Y)) return false;
        bool hasGround = location.hasTileAt(tile.X, tile.Y, "Back");
        bool passable = hasGround && location.isTilePassable(tile.ToVector2());
        return WorkerCombatTilePolicy.IsSafeRouteStep(true, hasGround, passable,
            location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null,
            location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y));
    }

    private bool TryChooseConnectedLandingNearFarmer(GameLocation location, NPC worker, int maxRadius, out Point landing)
    {
        Point origin = Game1.player.TilePoint;
        bool found = WorkerCombatTilePolicy.TryChooseConnectedLanding(origin.X, origin.Y, maxRadius,
            (x, y) => this.IsSafeLandingTile(location, worker, new Point(x, y)), out (int X, int Y) tile);
        landing = found ? new Point(tile.X, tile.Y) : Point.Zero;
        return found;
    }

    private bool TryChooseFloorLanding(GameLocation location, NPC worker, out Point landing)
    {
        var ground = location.map.RequireLayer("Back");
        Point origin = Game1.player.TilePoint;
        bool found = WorkerCombatTilePolicy.TryChooseFloorLanding(ground.LayerWidth, ground.LayerHeight,
            origin.X, origin.Y,
            (x, y) => this.IsSafeRouteStep(location, new Point(x, y))
                && this.navigation.IsTraversableWorkTile(location, worker, new Point(x, y)),
            (x, y) => this.IsSafeLandingTile(location, worker, new Point(x, y)), out var tile);
        landing = found ? new Point(tile.X, tile.Y) : Point.Zero;
        return found;
    }
}
