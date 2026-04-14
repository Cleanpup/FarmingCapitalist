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
    private sealed class ActiveWorkerRoute
    {
        public WorkerNavigationTarget Target { get; init; } = new WorkerNavigationTarget(string.Empty, Point.Zero, 2);

        public string TriggerReason { get; init; } = string.Empty;

        public string? LastObservedLocationName { get; set; }

        public Vector2 LastObservedPosition { get; set; }

        public int RouteStartTick { get; init; }

        public int StalledTicks { get; set; }
    }

    private const int MaxStalledTicks = 600;
    private readonly Dictionary<string, ActiveWorkerRoute> activeRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;

    public WorkerNavigationManager(WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public bool TryStartTravel(NPC worker, WorkerNavigationTarget target, string triggerReason)
    {
        if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
        {
            this.monitor.Log("Worker navigation couldn't start because the worker was missing a managed worker ID.", LogLevel.Warn);
            return false;
        }

        this.StopWorkerNavigation(worker);
        this.ClearActiveRoute(workerId);

        if (worker.currentLocation is null)
        {
            this.monitor.Log("Worker navigation couldn't start because the worker has no current location.", LogLevel.Warn);
            return false;
        }

        if (this.IsAtTarget(worker, target))
        {
            this.monitor.Log(
                $"{worker.displayName} is already at {target.LocationName} tile {target.Tile}; no navigation start was needed.",
                LogLevel.Info);
            return true;
        }

        if (Game1.getLocationFromName(target.LocationName) is null)
        {
            this.monitor.Log(
                $"Worker navigation couldn't start because destination location '{target.LocationName}' was not found.",
                LogLevel.Warn);
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
                $"{worker.displayName} navigation threw while building a route to {target.LocationName} tile {target.Tile}: {ex.Message}",
                LogLevel.Warn);
        }

        if (!this.HasUsableRoute(routeDescription)
            && this.TryBuildFallbackRoute(worker, target, out SchedulePathDescription fallbackRoute, out string fallbackDescription))
        {
            routeDescription = fallbackRoute;
            this.monitor.Log(
                $"{worker.displayName} navigation is using fallback routing for {fallbackDescription}.",
                LogLevel.Info);
        }

        if (!this.HasUsableRoute(routeDescription))
        {
            this.monitor.Log(
                $"{worker.displayName} navigation failed to build a route from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile}.",
                LogLevel.Warn);
            if (vanillaRouteException is not null)
            {
                this.monitor.Log(
                    $"Vanilla route builder exception for diagnostics: {vanillaRouteException.Message}",
                    LogLevel.Warn);
            }
            this.LogRouteFailureDiagnostics(worker, target);
            return false;
        }

        SchedulePathDescription finalRouteDescription = routeDescription!;

        worker.nextEndOfRouteMessage = null;
        worker.DirectionsToNewLocation = finalRouteDescription;
        worker.controller = new PathFindController(finalRouteDescription.route, worker, worker.currentLocation)
        {
            finalFacingDirection = finalRouteDescription.facingDirection,
        };

        this.activeRoutes[workerId] = new ActiveWorkerRoute
        {
            Target = target,
            TriggerReason = triggerReason,
            LastObservedLocationName = worker.currentLocation.NameOrUniqueName,
            LastObservedPosition = worker.Position,
            RouteStartTick = Game1.ticks,
            StalledTicks = 0,
        };

        this.monitor.Log(
            $"{worker.displayName} navigation started from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile} ({finalRouteDescription.route.Count} queued steps, trigger: {triggerReason}).",
            LogLevel.Info);
        return true;
    }

    public void Update()
    {
        if (this.activeRoutes.Count == 0)
        {
            return;
        }

        if (!Context.IsWorldReady)
        {
            this.activeRoutes.Clear();
            return;
        }

        foreach (string workerId in new List<string>(this.activeRoutes.Keys))
        {
            ActiveWorkerRoute route = this.activeRoutes[workerId];
            if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
            {
                this.monitor.Log($"Worker navigation was cleared because worker '{workerId}' could no longer be found.", LogLevel.Warn);
                this.ClearActiveRoute(workerId);
                continue;
            }

            if (this.IsAtTarget(worker, route.Target))
            {
                int elapsedTicks = Math.Max(0, Game1.ticks - route.RouteStartTick);
                this.SnapWorkerToIdleFacing(worker, route.Target.FacingDirection);
                this.monitor.Log(
                    $"{worker.displayName} navigation reached {route.Target.LocationName} tile {route.Target.Tile} after {elapsedTicks} ticks (trigger: {route.TriggerReason}).",
                    LogLevel.Info);
                this.ClearActiveRoute(workerId);
                continue;
            }

            if (worker.controller is null)
            {
                this.monitor.Log(
                    $"{worker.displayName} navigation ended before reaching {route.Target.LocationName} tile {route.Target.Tile}. Current position is {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}.",
                    LogLevel.Warn);
                this.StopWorkerNavigation(worker);
                this.ClearActiveRoute(workerId);
                continue;
            }

            if (worker.currentLocation != Game1.currentLocation)
            {
                route.LastObservedLocationName = worker.currentLocation?.NameOrUniqueName;
                route.LastObservedPosition = worker.Position;
                route.StalledTicks = 0;
                continue;
            }

            string currentLocationName = worker.currentLocation?.NameOrUniqueName ?? "unknown";
            if (currentLocationName == route.LastObservedLocationName && worker.Position == route.LastObservedPosition)
            {
                route.StalledTicks++;
            }
            else
            {
                route.LastObservedLocationName = currentLocationName;
                route.LastObservedPosition = worker.Position;
                route.StalledTicks = 0;
            }

            if (route.StalledTicks < MaxStalledTicks)
            {
                continue;
            }

            this.monitor.Log(
                $"{worker.displayName} navigation stalled at {currentLocationName} tile {worker.TilePoint} while travelling to {route.Target.LocationName} tile {route.Target.Tile}. Cancelling the current route.",
                LogLevel.Warn);
            this.StopWorkerNavigation(worker);
            this.ClearActiveRoute(workerId);
        }
    }

    public void Reset()
    {
        if (Context.IsWorldReady)
        {
            foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
            {
                this.StopWorkerNavigation(worker);
            }
        }

        this.activeRoutes.Clear();
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

    private void ClearActiveRoute(string workerId)
    {
        this.activeRoutes.Remove(workerId);
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

                Stack<Point>? segmentPath = this.FindCollisionAwarePath(locationStartPoint, warpPoint, currentLocation, worker);
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

    private Stack<Point> AddToStackForSchedule(Stack<Point> schedulePath, Stack<Point> segmentPath)
    {
        schedulePath = new Stack<Point>(schedulePath);
        while (schedulePath.Count > 0)
        {
            segmentPath.Push(schedulePath.Pop());
        }

        return segmentPath;
    }

    private Stack<Point>? FindCollisionAwarePath(Point startTile, Point endTile, GameLocation location, NPC worker)
    {
        Stack<Point>? collisionAwarePath = PathFindController.findPath(
            startTile,
            endTile,
            PathFindController.isAtEndPoint,
            location,
            worker,
            30000);
        if (collisionAwarePath is not null && collisionAwarePath.Count > 0)
        {
            return collisionAwarePath;
        }

        return PathFindController.findPathForNPCSchedules(startTile, endTile, location, 30000);
    }

    private bool TryResolveWarpTarget(GameLocation currentLocation, string nextLocationName, Point warpPoint, NPC worker, out Point warpTarget)
    {
        warpTarget = Point.Zero;

        Warp? doorWarp = currentLocation.getWarpFromDoor(warpPoint, worker);
        if (doorWarp is not null
            && string.Equals(doorWarp.TargetName, nextLocationName, StringComparison.OrdinalIgnoreCase))
        {
            warpTarget = new Point(doorWarp.TargetX, doorWarp.TargetY);
            return true;
        }

        foreach (Warp warp in currentLocation.warps)
        {
            if (warp.X != warpPoint.X || warp.Y != warpPoint.Y)
            {
                continue;
            }

            if (!string.Equals(warp.TargetName, nextLocationName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            warpTarget = currentLocation.getWarpPointTarget(warpPoint, worker);
            return true;
        }

        return false;
    }

    private void LogRouteFailureDiagnostics(NPC worker, WorkerNavigationTarget target)
    {
        if (worker.currentLocation is null)
        {
            return;
        }

        bool destinationLocationFound = Game1.getLocationFromName(target.LocationName) is not null;
        bool destinationTileOnMap = destinationLocationFound
            && Game1.RequireLocation(target.LocationName).isTileOnMap(target.Tile.ToVector2());
        bool startTileBlocked = worker.currentLocation.IsTileBlockedBy(worker.Tile, CollisionMask.All, CollisionMask.Characters, useFarmerTile: true);
        bool targetTileBlocked = destinationLocationFound
            && Game1.RequireLocation(target.LocationName).IsTileBlockedBy(target.Tile.ToVector2(), CollisionMask.All, CollisionMask.Characters, useFarmerTile: true);

        this.monitor.Log(
            $"Route diagnostics for {worker.displayName}: destination location found={destinationLocationFound}, destination tile on map={destinationTileOnMap}, start tile blocked={startTileBlocked}, target tile blocked={targetTileBlocked}.",
            LogLevel.Trace);
    }
}
