using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Pathfinding;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerNavigationManager
{
    private const int MaxStalledTicks = 600;
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;
    private WorkerNavigationTarget? activeTarget;
    private string? activeTriggerReason;
    private string? lastObservedLocationName;
    private Vector2 lastObservedPosition;
    private int routeStartTick;
    private int stalledTicks;

    public WorkerNavigationManager(WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public bool TryStartTravel(NPC worker, WorkerNavigationTarget target, string triggerReason)
    {
        this.StopWorkerNavigation(worker);

        if (worker.currentLocation is null)
        {
            this.monitor.Log("Worker navigation couldn't start because the worker has no current location.", LogLevel.Warn);
            this.ClearActiveRoute();
            return false;
        }

        if (this.IsAtTarget(worker, target))
        {
            this.monitor.Log(
                $"Worker is already at {target.LocationName} tile {target.Tile}; no navigation start was needed.",
                LogLevel.Info);
            this.ClearActiveRoute();
            return true;
        }

        if (Game1.getLocationFromName(target.LocationName) is null)
        {
            this.monitor.Log(
                $"Worker navigation couldn't start because destination location '{target.LocationName}' was not found.",
                LogLevel.Warn);
            this.ClearActiveRoute();
            return false;
        }

        SchedulePathDescription? routeDescription = null;
        Exception? vanillaRouteException = null;
        try
        {
            routeDescription = worker.pathfindToNextScheduleLocation(
                "farmingcapitalist_runtime",
                worker.currentLocation.NameOrUniqueName,
                worker.TilePoint.X,
                worker.TilePoint.Y,
                target.LocationName,
                target.Tile.X,
                target.Tile.Y,
                target.FacingDirection,
                endBehavior: null,
                endMessage: null);
        }
        catch (Exception ex)
        {
            vanillaRouteException = ex;
            this.monitor.Log(
                $"Worker navigation threw while building a route to {target.LocationName} tile {target.Tile}: {ex.Message}",
                LogLevel.Warn);
        }

        if (!this.HasUsableRoute(routeDescription)
            && this.TryBuildFallbackRoute(worker, target, out SchedulePathDescription fallbackRoute, out string fallbackDescription))
        {
            routeDescription = fallbackRoute;
            this.monitor.Log(
                $"Worker navigation is using fallback routing for {fallbackDescription}.",
                LogLevel.Info);
        }

        if (!this.HasUsableRoute(routeDescription))
        {
            this.monitor.Log(
                $"Worker navigation failed to build a route from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile}.",
                LogLevel.Warn);
            if (vanillaRouteException is not null)
            {
                this.monitor.Log(
                    $"Vanilla route builder exception for diagnostics: {vanillaRouteException.Message}",
                    LogLevel.Warn);
            }
            this.LogRouteFailureDiagnostics(worker, target);
            this.ClearActiveRoute();
            return false;
        }

        SchedulePathDescription finalRouteDescription = routeDescription!;

        worker.nextEndOfRouteMessage = null;
        worker.DirectionsToNewLocation = finalRouteDescription;
        worker.controller = new PathFindController(finalRouteDescription.route, worker, worker.currentLocation)
        {
            finalFacingDirection = finalRouteDescription.facingDirection,
        };

        this.activeTarget = target;
        this.activeTriggerReason = triggerReason;
        this.lastObservedLocationName = worker.currentLocation.NameOrUniqueName;
        this.lastObservedPosition = worker.Position;
        this.routeStartTick = Game1.ticks;
        this.stalledTicks = 0;

        this.monitor.Log(
            $"Worker navigation started from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile} ({finalRouteDescription.route.Count} queued steps, trigger: {triggerReason}).",
            LogLevel.Info);
        return true;
    }

    public void Update()
    {
        if (this.activeTarget is null)
        {
            return;
        }

        if (!Context.IsWorldReady || !this.workerShellManager.TryGetTestWorker(out NPC? worker) || worker is null)
        {
            this.monitor.Log("Worker navigation was cleared because the worker could no longer be found.", LogLevel.Warn);
            this.ClearActiveRoute();
            return;
        }

        if (this.IsAtTarget(worker, this.activeTarget))
        {
            int elapsedTicks = Math.Max(0, Game1.ticks - this.routeStartTick);
            this.SnapWorkerToIdleFacing(worker, this.activeTarget.FacingDirection);
            this.monitor.Log(
                $"Worker navigation reached {this.activeTarget.LocationName} tile {this.activeTarget.Tile} after {elapsedTicks} ticks (trigger: {this.activeTriggerReason}).",
                LogLevel.Info);
            this.ClearActiveRoute();
            return;
        }

        if (worker.controller is null)
        {
            this.monitor.Log(
                $"Worker navigation ended before reaching {this.activeTarget.LocationName} tile {this.activeTarget.Tile}. Current position is {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}.",
                LogLevel.Warn);
            this.StopWorkerNavigation(worker);
            this.ClearActiveRoute();
            return;
        }

        if (worker.currentLocation != Game1.currentLocation)
        {
            this.lastObservedLocationName = worker.currentLocation?.NameOrUniqueName;
            this.lastObservedPosition = worker.Position;
            this.stalledTicks = 0;
            return;
        }

        string currentLocationName = worker.currentLocation?.NameOrUniqueName ?? "unknown";
        if (currentLocationName == this.lastObservedLocationName && worker.Position == this.lastObservedPosition)
        {
            this.stalledTicks++;
        }
        else
        {
            this.lastObservedLocationName = currentLocationName;
            this.lastObservedPosition = worker.Position;
            this.stalledTicks = 0;
        }

        if (this.stalledTicks < MaxStalledTicks)
        {
            return;
        }

        this.monitor.Log(
            $"Worker navigation stalled at {currentLocationName} tile {worker.TilePoint} while travelling to {this.activeTarget.LocationName} tile {this.activeTarget.Tile}. Cancelling the current route.",
            LogLevel.Warn);
        this.StopWorkerNavigation(worker);
        this.ClearActiveRoute();
    }

    public void Reset()
    {
        if (Context.IsWorldReady && this.workerShellManager.TryGetTestWorker(out NPC? worker) && worker is not null)
        {
            this.StopWorkerNavigation(worker);
        }

        this.ClearActiveRoute();
    }

    private bool IsAtTarget(NPC worker, WorkerNavigationTarget target)
    {
        return worker.currentLocation?.NameOrUniqueName == target.LocationName
            && worker.TilePoint == target.Tile;
    }

    private bool HasUsableRoute(SchedulePathDescription? routeDescription)
    {
        return routeDescription?.route is not null && routeDescription.route.Count > 0;
    }

    private void StopWorkerNavigation(NPC worker)
    {
        worker.Halt();
        worker.controller = null;
        worker.temporaryController = null;
        worker.DirectionsToNewLocation = null;
    }

    private void ClearActiveRoute()
    {
        this.activeTarget = null;
        this.activeTriggerReason = null;
        this.lastObservedLocationName = null;
        this.lastObservedPosition = Vector2.Zero;
        this.routeStartTick = 0;
        this.stalledTicks = 0;
    }

    private void SnapWorkerToIdleFacing(NPC worker, int facingDirection)
    {
        worker.Halt();
        worker.FacingDirection = facingDirection;
        worker.Sprite?.faceDirectionStandard(facingDirection);
    }

    private bool TryBuildFallbackRoute(NPC worker, WorkerNavigationTarget target, out SchedulePathDescription routeDescription, out string fallbackDescription)
    {
        routeDescription = new SchedulePathDescription(new Stack<Point>(), target.FacingDirection, null, null, target.LocationName, target.Tile);
        fallbackDescription = string.Empty;

        if (worker.currentLocation is null)
        {
            return false;
        }

        string[]? locationRoute = this.TryGetFallbackLocationRoute(worker.currentLocation.NameOrUniqueName, target.LocationName);
        if (locationRoute is null)
        {
            return false;
        }

        Stack<Point>? route = this.BuildManualScheduleRoute(worker, worker.TilePoint, locationRoute, target);
        if (route is null || route.Count == 0)
        {
            return false;
        }

        routeDescription = new SchedulePathDescription(route, target.FacingDirection, null, null, target.LocationName, target.Tile);
        fallbackDescription = string.Join(" -> ", locationRoute);
        return true;
    }

    private string[]? TryGetFallbackLocationRoute(string startLocationName, string targetLocationName)
    {
        if (startLocationName == "FarmHouse" && targetLocationName == "Farm")
        {
            return new[] { "FarmHouse", "Farm" };
        }

        if (startLocationName == "Farm" && targetLocationName == "FarmHouse")
        {
            return new[] { "Farm", "FarmHouse" };
        }

        return null;
    }

    private Stack<Point>? BuildManualScheduleRoute(NPC worker, Point startTile, string[] locationRoute, WorkerNavigationTarget target)
    {
        Stack<Point> path = new();
        Point locationStartPoint = startTile;

        for (int i = 0; i < locationRoute.Length; i++)
        {
            GameLocation currentLocation = Game1.RequireLocation(locationRoute[i]);
            if (i < locationRoute.Length - 1)
            {
                string nextLocationName = locationRoute[i + 1];
                Point warpPoint = currentLocation.getWarpPointTo(nextLocationName, worker);
                if (warpPoint == Point.Zero)
                {
                    this.monitor.Log(
                        $"Fallback routing failed because {currentLocation.NameOrUniqueName} has no warp point to {nextLocationName}.",
                        LogLevel.Warn);
                    return null;
                }

                Stack<Point>? segmentPath = this.FindCollisionAwareWarpPath(locationStartPoint, warpPoint, currentLocation, worker);
                if (segmentPath is null || segmentPath.Count == 0)
                {
                    this.monitor.Log(
                        $"Fallback routing couldn't build a collision-aware local path in {currentLocation.NameOrUniqueName} from {locationStartPoint} to warp {warpPoint}.",
                        LogLevel.Warn);
                    return null;
                }

                path = this.AddToStackForSchedule(path, segmentPath);
                if (!this.TryResolveWarpTarget(currentLocation, nextLocationName, warpPoint, worker, out Point warpTarget))
                {
                    this.monitor.Log(
                        $"Fallback routing failed because warp {warpPoint} in {currentLocation.NameOrUniqueName} had no target tile for {nextLocationName}.",
                        LogLevel.Warn);
                    return null;
                }

                locationStartPoint = warpTarget;
                continue;
            }

            Stack<Point>? finalSegmentPath = this.FindCollisionAwarePath(locationStartPoint, target.Tile, currentLocation, worker);
            if (finalSegmentPath is null || finalSegmentPath.Count == 0)
            {
                this.monitor.Log(
                    $"Fallback routing couldn't build a collision-aware final local path in {currentLocation.NameOrUniqueName} from {locationStartPoint} to {target.Tile}.",
                    LogLevel.Warn);
                return null;
            }

            path = this.AddToStackForSchedule(path, finalSegmentPath);
        }

        return path;
    }

    private Stack<Point> AddToStackForSchedule(Stack<Point> original, Stack<Point> toAdd)
    {
        original = new Stack<Point>(original);
        while (original.Count > 0)
        {
            toAdd.Push(original.Pop());
        }

        return toAdd;
    }

    private Stack<Point>? FindCollisionAwareWarpPath(Point startTile, Point warpTile, GameLocation location, NPC worker)
    {
        Stack<Point>? directPath = this.FindCollisionAwarePath(startTile, warpTile, location, worker);
        if (directPath is not null && directPath.Count > 0)
        {
            return directPath;
        }

        foreach (Point approachTile in this.GetWarpApproachCandidates(warpTile, location, worker))
        {
            Stack<Point>? approachPath = this.FindCollisionAwarePath(startTile, approachTile, location, worker);
            if (approachPath is null || approachPath.Count == 0)
            {
                continue;
            }

            this.monitor.Log(
                $"Fallback routing is approaching warp {warpTile} in {location.NameOrUniqueName} via adjacent tile {approachTile}.",
                LogLevel.Trace);
            return this.AppendPointAtEnd(approachPath, warpTile);
        }

        return null;
    }

    private Stack<Point>? FindCollisionAwarePath(Point startTile, Point endTile, GameLocation location, NPC worker)
    {
        return PathFindController.findPath(
            startTile,
            endTile,
            PathFindController.isAtEndPoint,
            location,
            worker,
            30000);
    }

    private Stack<Point>? FindSchedulePath(Point startTile, Point endTile, GameLocation location)
    {
#pragma warning disable CS0618
        return PathFindController.findPathForNPCSchedules(startTile, endTile, location, 30000);
#pragma warning restore CS0618
    }

    private IEnumerable<Point> GetWarpApproachCandidates(Point warpTile, GameLocation location, NPC worker)
    {
        Point[] candidates =
        {
            new(warpTile.X, warpTile.Y + 1),
            new(warpTile.X - 1, warpTile.Y),
            new(warpTile.X + 1, warpTile.Y),
            new(warpTile.X, warpTile.Y - 1),
        };

        foreach (Point candidate in candidates)
        {
            Vector2 candidateVector = new(candidate.X, candidate.Y);
            if (!location.isTileOnMap(candidateVector))
            {
                continue;
            }

            if (location.isCollidingPosition(
                    new Rectangle(candidate.X * Game1.tileSize + 1, candidate.Y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2),
                    Game1.viewport,
                    isFarmer: false,
                    0,
                    glider: false,
                    worker,
                    pathfinding: true))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private Stack<Point> AppendPointAtEnd(Stack<Point> path, Point endPoint)
    {
        List<Point> orderedPoints = new(path);
        orderedPoints.Add(endPoint);
        return new Stack<Point>(orderedPoints.AsEnumerable().Reverse());
    }

    private bool TryResolveWarpTarget(GameLocation currentLocation, string nextLocationName, Point warpPoint, NPC worker, out Point warpTarget)
    {
        warpTarget = currentLocation.getWarpPointTarget(warpPoint, worker);
        if (warpTarget != Point.Zero)
        {
            return true;
        }

        foreach (Building building in currentLocation.buildings)
        {
            if (!building.HasIndoorsName(nextLocationName) || building.getPointForHumanDoor() != warpPoint)
            {
                continue;
            }

            GameLocation? indoors = building.GetIndoors();
            if (indoors is FarmHouse farmHouse)
            {
                warpTarget = farmHouse.getEntryLocation();
                return warpTarget != Point.Zero;
            }

            if (indoors is not null)
            {
                warpTarget = indoors.getWarpPointTo(currentLocation.NameOrUniqueName, worker);
                if (warpTarget != Point.Zero)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void LogRouteFailureDiagnostics(NPC worker, WorkerNavigationTarget target)
    {
        GameLocation? startLocation = worker.currentLocation;
        if (startLocation is null)
        {
            return;
        }

        Point startTile = worker.TilePoint;
        this.monitor.Log(
            $"Route diagnostics: start {startLocation.NameOrUniqueName} {startTile}, destination {target.LocationName} {target.Tile}.",
            LogLevel.Warn);
        this.monitor.Log(
            $"Start tile: {this.DescribeScheduleTile(startLocation, startTile, worker, allowWarpEndpoint: false)}",
            LogLevel.Warn);

        GameLocation? destinationLocation = Game1.getLocationFromName(target.LocationName);
        if (destinationLocation is not null)
        {
            this.monitor.Log(
                $"Destination tile: {this.DescribeScheduleTile(destinationLocation, target.Tile, worker, allowWarpEndpoint: true)}",
                LogLevel.Warn);
        }

        if (startLocation.NameOrUniqueName == target.LocationName)
        {
            this.LogSegmentDiagnostics(startLocation, startTile, target.Tile, worker, finalDestinationName: target.LocationName);
            return;
        }

        WarpPathfindingCache.PopulateCache();
        string[]? locationRoute = WarpPathfindingCache.GetLocationRoute(startLocation.NameOrUniqueName, target.LocationName, worker.Gender);
        if (locationRoute is null)
        {
            bool destinationIgnored = WarpPathfindingCache.IgnoreLocationNames.Contains(target.LocationName);
            bool startIgnored = WarpPathfindingCache.IgnoreLocationNames.Contains(startLocation.NameOrUniqueName);
            this.monitor.Log(
                $"No cross-location route was available from {startLocation.NameOrUniqueName} to {target.LocationName}. " +
                $"Warp cache ignores start={startIgnored}, destination={destinationIgnored}. Ignore list includes: {string.Join(", ", WarpPathfindingCache.IgnoreLocationNames)}.",
                LogLevel.Warn);
            return;
        }

        this.monitor.Log($"Location route candidate: {string.Join(" -> ", locationRoute)}.", LogLevel.Warn);

        Point segmentStart = startTile;
        for (int i = 0; i < locationRoute.Length; i++)
        {
            GameLocation currentLocation = Game1.RequireLocation(locationRoute[i]);
            if (i < locationRoute.Length - 1)
            {
                string nextLocationName = locationRoute[i + 1];
                Point warpPoint = currentLocation.getWarpPointTo(nextLocationName, worker);
                if (warpPoint == Point.Zero)
                {
                    this.monitor.Log(
                        $"Segment diagnostics: {currentLocation.NameOrUniqueName} has no warp point to {nextLocationName}.",
                        LogLevel.Warn);
                    return;
                }

                if (!this.LogSegmentDiagnostics(currentLocation, segmentStart, warpPoint, worker, finalDestinationName: nextLocationName))
                {
                    return;
                }

                Point warpTarget = currentLocation.getWarpPointTarget(warpPoint, worker);
                this.monitor.Log(
                    $"Warp handoff: {currentLocation.NameOrUniqueName} {warpPoint} -> {nextLocationName} {warpTarget}.",
                    LogLevel.Warn);
                segmentStart = warpTarget;
                continue;
            }

            this.LogSegmentDiagnostics(currentLocation, segmentStart, target.Tile, worker, finalDestinationName: target.LocationName);
        }
    }

    private bool LogSegmentDiagnostics(GameLocation location, Point startTile, Point endTile, NPC worker, string finalDestinationName)
    {
        Stack<Point>? segmentPath = this.FindSchedulePath(startTile, endTile, location);
        if (segmentPath is null || segmentPath.Count == 0)
        {
            this.monitor.Log(
                $"Segment diagnostics: no local NPC-schedule path in {location.NameOrUniqueName} from {startTile} to {endTile} while heading toward {finalDestinationName}.",
                LogLevel.Warn);
            this.monitor.Log(
                $"Segment start tile: {this.DescribeScheduleTile(location, startTile, worker, allowWarpEndpoint: false)}",
                LogLevel.Warn);
            this.monitor.Log(
                $"Segment end tile: {this.DescribeScheduleTile(location, endTile, worker, allowWarpEndpoint: true)}",
                LogLevel.Warn);
            return false;
        }

        this.monitor.Log(
            $"Segment diagnostics: {location.NameOrUniqueName} path from {startTile} to {endTile} succeeded with {segmentPath.Count} steps.",
            LogLevel.Warn);
        return true;
    }

    private string DescribeScheduleTile(GameLocation location, Point tile, NPC worker, bool allowWarpEndpoint)
    {
        bool onMap = location.isTileOnMap(new Vector2(tile.X, tile.Y));
        if (!onMap)
        {
            return $"tile {tile} is off-map for {location.NameOrUniqueName}.";
        }

        List<string> notes = new()
        {
            $"tile={tile}",
            $"buildingIndex={location.getTileIndexAt(tile, "Buildings")}",
        };

        string? action = location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings");
        if (!string.IsNullOrWhiteSpace(action))
        {
            notes.Add($"action={action}");
        }

        if (location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Buildings") is not null)
        {
            notes.Add("passable=true");
        }

        if (location.doesTileHaveProperty(tile.X, tile.Y, "NPCPassable", "Buildings") is not null)
        {
            notes.Add("npcPassable=true");
        }

        if (location.doesTileHaveProperty(tile.X, tile.Y, "NoPath", "Back") is not null)
        {
            notes.Add("noPath=true");
        }

        if (location.isTerrainFeatureAt(tile.X, tile.Y))
        {
            notes.Add("terrainFeature=true");
        }

        bool warpTile = location.getWarpPointTarget(tile, worker) != Point.Zero;
        if (warpTile)
        {
            notes.Add($"warpTarget={location.getWarpPointTarget(tile, worker)}");
        }

        bool scheduleImpassable = this.IsScheduleImpassable(location, tile, warpTile && allowWarpEndpoint);
        notes.Add($"scheduleImpassable={scheduleImpassable}");

        return string.Join(", ", notes);
    }

    private bool IsScheduleImpassable(GameLocation location, Point tile, bool allowWarpEndpoint)
    {
        if (!location.isTileOnMap(new Vector2(tile.X, tile.Y)))
        {
            return true;
        }

        int buildingTileIndex = location.getTileIndexAt(tile, "Buildings");
        if (buildingTileIndex != -1)
        {
            string? action = location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings");
            if (!string.IsNullOrEmpty(action))
            {
                if (action.StartsWith("LockedDoorWarp", StringComparison.Ordinal))
                {
                    return true;
                }

                if (!action.Contains("Door", StringComparison.Ordinal) && !action.Contains("Passable", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Buildings") is null
                && location.doesTileHaveProperty(tile.X, tile.Y, "NPCPassable", "Buildings") is null)
            {
                return true;
            }
        }

        if (location.doesTileHaveProperty(tile.X, tile.Y, "NoPath", "Back") is not null)
        {
            return true;
        }

        if (!allowWarpEndpoint && location.getWarpPointTarget(tile) != Point.Zero)
        {
            return true;
        }

        return location.isTerrainFeatureAt(tile.X, tile.Y);
    }
}
