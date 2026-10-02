using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerNavigationManager
{
    private sealed class PlannedWarpTransition
    {
        public string TargetLocationName { get; init; } = string.Empty;

        public Point TargetTile { get; init; }

        public Point ActivationTile { get; init; }

        public Point WarpTile { get; init; }
    }

    private sealed class ActiveWorkerRoute
    {
        public WorkerNavigationTarget Target { get; init; } = new WorkerNavigationTarget(string.Empty, Point.Zero, 2);

        public string TriggerReason { get; init; } = string.Empty;

        public string? LastObservedLocationName { get; set; }

        public string LegStartLocationName { get; init; } = string.Empty;

        public Vector2 LastObservedPosition { get; set; }

        public int RouteStartTick { get; init; }

        public int StalledTicks { get; set; }

        public int RetryCount { get; set; }

        public PathFindController? Controller { get; set; }

        public PlannedWarpTransition? WarpTransition { get; init; }
    }

    private const int MaxStalledTicks = 180;
    private const int MaxRouteRetries = 2;
    private readonly Dictionary<string, ActiveWorkerRoute> activeRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;

    public WorkerNavigationManager(WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public bool TryGetLocalRouteLength(NPC worker, Point target, out int routeLength)
    {
        routeLength = 0;
        if (!Context.IsWorldReady
            || !Context.IsMainPlayer
            || worker.currentLocation is null)
        {
            return false;
        }

        Stack<Point>? path = this.FindCollisionAwarePath(worker.TilePoint, target, worker.currentLocation, worker);
        if (path is null || path.Count == 0)
        {
            return false;
        }

        routeLength = path.Count;
        return true;
    }

    public bool TryStartTravel(NPC worker, WorkerNavigationTarget target, string triggerReason, string? avoidFirstHopLocation = null)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.HasForeignController(worker))
        {
            return false;
        }

        if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
        {
            this.monitor.Log("Worker navigation couldn't start because the worker was missing a managed worker ID.", LogLevel.Trace);
            return false;
        }

        this.StopTravel(worker);

        if (worker.currentLocation is null)
        {
            this.monitor.Log("Worker navigation couldn't start because the worker has no current location.", LogLevel.Trace);
            return false;
        }

        if (this.IsAtTarget(worker, target))
        {
            this.monitor.Log(
                $"{worker.displayName} is already at {target.LocationName} tile {target.Tile}; no navigation start was needed.",
                LogLevel.Trace);
            return true;
        }

        GameLocation? destination = Game1.getLocationFromName(target.LocationName);
        if (destination is null || !destination.isTileOnMap(target.Tile.ToVector2()))
        {
            this.monitor.Log(
                $"Worker navigation couldn't start because destination location '{target.LocationName}' was not found.",
                LogLevel.Trace);
            return false;
        }

        SchedulePathDescription? routeDescription = null;
        PlannedWarpTransition? warpTransition = null;
        try
        {
            if (worker.currentLocation == destination)
            {
                Stack<Point>? path = this.FindCollisionAwarePath(worker.TilePoint, target.Tile, destination, worker);
                if (path is { Count: > 0 })
                {
                    routeDescription = new SchedulePathDescription(path, target.FacingDirection, null, null, target.LocationName, target.Tile);
                }
            }
            else if (this.GetFallbackLocationRoutes(worker.currentLocation.NameOrUniqueName, target.LocationName, avoidFirstHopLocation).Count > 0)
            {
                if (this.TryBuildFallbackRoute(
                        worker,
                        target,
                        avoidFirstHopLocation,
                        out SchedulePathDescription fallbackRoute,
                        out _,
                        out PlannedWarpTransition? fallbackWarpTransition))
                {
                    routeDescription = fallbackRoute;
                    warpTransition = fallbackWarpTransition;
                }
            }
            else
            {
                routeDescription = worker.pathfindToNextScheduleLocation(
                    "farmingcapitalist_runtime", worker.currentLocation.NameOrUniqueName,
                    worker.TilePoint.X, worker.TilePoint.Y, target.LocationName,
                    target.Tile.X, target.Tile.Y, target.FacingDirection, endBehavior: null, endMessage: null);
            }
        }
        catch (Exception ex)
        {
            this.monitor.Log($"{worker.displayName} could not plan travel to {target.LocationName} {target.Tile}: {ex.Message}", LogLevel.Trace);
        }

        if (!this.HasUsableRoute(routeDescription))
        {
            this.monitor.Log(
                $"{worker.displayName} navigation failed to build a route from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile}.",
                LogLevel.Trace);
            this.LogRouteFailureDiagnostics(worker, target);
            return false;
        }

        SchedulePathDescription finalRouteDescription = routeDescription!;

        Point firstStep = finalRouteDescription.route.Peek();
        Point lastStep = finalRouteDescription.route.Last();
        this.monitor.Log(
            $"{worker.displayName} route built: from {worker.TilePoint} to {target.LocationName} {target.Tile}; "
            + $"steps={finalRouteDescription.route.Count}, firstStep={firstStep}, lastStep={lastStep}, "
            + $"sameLocation={worker.currentLocation == destination}, trigger={triggerReason}.",
            LogLevel.Trace);

        worker.nextEndOfRouteMessage = null;
        worker.DirectionsToNewLocation = finalRouteDescription;
        worker.controller = new PathFindController(finalRouteDescription.route, worker, worker.currentLocation)
        {
            finalFacingDirection = finalRouteDescription.facingDirection,
            nonDestructivePathing = true,
        };

        this.activeRoutes[workerId] = new ActiveWorkerRoute
        {
            Target = target,
            TriggerReason = triggerReason,
            LastObservedLocationName = worker.currentLocation.NameOrUniqueName,
            LegStartLocationName = worker.currentLocation.NameOrUniqueName,
            LastObservedPosition = worker.Position,
            RouteStartTick = Game1.ticks,
            StalledTicks = 0,
            Controller = worker.controller,
            WarpTransition = warpTransition,
        };

        this.monitor.Log(
            $"{worker.displayName} navigation started from {worker.currentLocation.NameOrUniqueName} tile {worker.TilePoint} to {target.LocationName} tile {target.Tile} ({finalRouteDescription.route.Count} queued steps, trigger: {triggerReason}).",
            LogLevel.Trace);
        return true;
    }

    public void Update()
    {
        if (this.activeRoutes.Count == 0)
        {
            return;
        }

        if (!Context.IsWorldReady || !Context.IsMainPlayer || !Game1.shouldTimePass())
        {
            return;
        }

        foreach (string workerId in new List<string>(this.activeRoutes.Keys))
        {
            ActiveWorkerRoute route = this.activeRoutes[workerId];
            if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
            {
                this.monitor.Log($"Worker navigation was cleared because worker '{workerId}' could no longer be found.", LogLevel.Trace);
                this.ClearActiveRoute(workerId);
                continue;
            }

            if (this.HasForeignController(worker))
            {
                // An event or another mod owns movement now; don't overwrite its controller.
                this.monitor.Log(
                    $"{worker.displayName} navigation lost control at {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}; "
                    + $"controller={worker.controller?.GetType().Name ?? "none"}, temporaryController={worker.temporaryController?.GetType().Name ?? "none"}.",
                    LogLevel.Trace);
                this.ClearActiveRoute(workerId);
                continue;
            }

            if (this.IsAtTarget(worker, route.Target))
            {
                int elapsedTicks = Math.Max(0, Game1.ticks - route.RouteStartTick);
                this.ApplyIdlePose(worker);
                this.monitor.Log(
                    $"{worker.displayName} navigation reached {route.Target.LocationName} tile {route.Target.Tile} after {elapsedTicks} ticks (trigger: {route.TriggerReason}).",
                    LogLevel.Trace);
                this.ClearActiveRoute(workerId);
                continue;
            }

            string currentLocationName = worker.currentLocation?.NameOrUniqueName ?? "unknown";
            bool legHasNoSteps = worker.controller?.pathToEndPoint is not { Count: > 0 };
            if (route.WarpTransition is PlannedWarpTransition transition
                && string.Equals(currentLocationName, route.LegStartLocationName, StringComparison.OrdinalIgnoreCase)
                && worker.TilePoint == transition.ActivationTile
                && legHasNoSteps)
            {
                string previousLocationName = route.LegStartLocationName;
                WorkerNavigationTarget finalTarget = route.Target;
                this.StopTravel(worker);
                Game1.warpCharacter(worker, transition.TargetLocationName, transition.TargetTile);
                this.monitor.Log(
                    $"{worker.displayName} completed warp leg {previousLocationName} {transition.WarpTile} -> "
                    + $"{transition.TargetLocationName} {transition.TargetTile} from reachable edge tile {transition.ActivationTile}.",
                    LogLevel.Trace);
                this.TryStartTravel(worker, finalTarget, "continue after location warp", previousLocationName);
                continue;
            }

            bool reachedNextLocation = !string.Equals(currentLocationName, route.LegStartLocationName, StringComparison.OrdinalIgnoreCase);
            if (reachedNextLocation && legHasNoSteps)
            {
                this.monitor.Log(
                    $"{worker.displayName} reached intermediate location {currentLocationName}; replanning the next collision-aware leg to {route.Target.LocationName} {route.Target.Tile}.",
                    LogLevel.Trace);
                this.StopTravel(worker);
                this.TryStartTravel(worker, route.Target, "continue after location warp", route.LegStartLocationName);
                continue;
            }

            if (worker.controller is null)
            {
                this.monitor.Log(
                    $"{worker.displayName} navigation ended before reaching {route.Target.LocationName} tile {route.Target.Tile}. "
                    + $"Current position is {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}; "
                    + $"route age={Game1.ticks - route.RouteStartTick} ticks, stalled={route.StalledTicks}, retry={route.RetryCount}.",
                    LogLevel.Trace);
                this.TryRecoverRoute(worker, workerId, route);
                continue;
            }

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

            if (Game1.ticks % 60 == 0)
            {
                string queuedPath = worker.controller.pathToEndPoint is { Count: > 0 } path
                    ? string.Join(" -> ", path.Take(8))
                    : "none";
                this.monitor.Log(
                    $"{worker.displayName} navigation progress: location={currentLocationName}, tile={worker.TilePoint}, "
                    + $"target={route.Target.LocationName} {route.Target.Tile}, controller={worker.controller.GetType().Name}, "
                    + $"remaining={worker.controller.pathToEndPoint?.Count ?? 0}, stalled={route.StalledTicks}, "
                    + $"elapsed={Game1.ticks - route.RouteStartTick} ticks, nextSteps={queuedPath}.",
                    LogLevel.Trace);
            }

            if (route.StalledTicks < MaxStalledTicks && Game1.ticks - route.RouteStartTick < 7200)
            {
                continue;
            }

            this.monitor.Log(
                $"{worker.displayName} navigation stalled at {currentLocationName} tile {worker.TilePoint} while travelling to {route.Target.LocationName} tile {route.Target.Tile}. Cancelling the current route.",
                LogLevel.Trace);
            this.TryRecoverRoute(worker, workerId, route);
        }
    }

    public bool HasActiveRoute(string workerId) => this.activeRoutes.ContainsKey(workerId);

    public bool HasForeignController(NPC worker)
    {
        if (worker.temporaryController is not null)
        {
            return true;
        }

        return worker.controller is not null
            && (!this.workerShellManager.TryGetWorkerId(worker, out string workerId)
                || !this.activeRoutes.TryGetValue(workerId, out ActiveWorkerRoute? route)
                || !ReferenceEquals(worker.controller, route.Controller));
    }

    public void StopTravel(NPC worker)
    {
        if (!Context.IsMainPlayer || !this.workerShellManager.TryGetWorkerId(worker, out string workerId))
        {
            return;
        }

        if (this.activeRoutes.TryGetValue(workerId, out ActiveWorkerRoute? route))
        {
            if (ReferenceEquals(worker.controller, route.Controller))
            {
                this.StopWorkerNavigation(worker);
            }

            this.ClearActiveRoute(workerId);
        }
    }

    private void TryRecoverRoute(NPC worker, string workerId, ActiveWorkerRoute previous)
    {
        this.StopTravel(worker);
        if (previous.RetryCount < MaxRouteRetries && this.TryStartTravel(worker, previous.Target, "replanning blocked route"))
        {
            if (this.activeRoutes.TryGetValue(workerId, out ActiveWorkerRoute? replacement))
            {
                replacement.RetryCount = previous.RetryCount + 1;
            }
        }
    }

    public void Reset()
    {
        if (Context.IsWorldReady && Context.IsMainPlayer)
        {
            foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
            {
                this.StopTravel(worker);
            }
        }

        this.activeRoutes.Clear();
    }

    private bool IsAtTarget(NPC worker, WorkerNavigationTarget target)
    {
        return worker.currentLocation?.NameOrUniqueName == target.LocationName
            && worker.TilePoint == target.Tile
            && (worker.controller is null || worker.controller.pathToEndPoint is not { Count: > 0 });
    }

    private bool HasUsableRoute(SchedulePathDescription? routeDescription)
    {
        return routeDescription?.route is not null && routeDescription.route.Count > 0;
    }

    public void StopWorker(NPC worker)
    {
        if (this.HasForeignController(worker))
        {
            return;
        }

        this.StopTravel(worker);
        this.StopWorkerNavigation(worker);
    }

    private void StopWorkerNavigation(NPC worker)
    {
        worker.Halt();
        worker.controller = null;
        worker.DirectionsToNewLocation = null;
    }

    private void ClearActiveRoute(string workerId)
    {
        this.activeRoutes.Remove(workerId);
    }

    public void ApplyIdlePose(NPC worker)
    {
        if (this.HasForeignController(worker))
        {
            return;
        }

        this.StopTravel(worker);
        this.StopWorkerNavigation(worker);
        worker.FacingDirection = TestWorkerDefinition.FacingDirection;
        if (worker.Sprite is not null)
        {
            worker.Sprite.StopAnimation();
            worker.Sprite.standAndFaceDirection(TestWorkerDefinition.FacingDirection);
            worker.Sprite.CurrentFrame = 0;
            worker.Sprite.UpdateSourceRect();
        }
    }

    private bool TryBuildFallbackRoute(
        NPC worker,
        WorkerNavigationTarget target,
        string? avoidFirstHopLocation,
        out SchedulePathDescription routeDescription,
        out string fallbackDescription,
        out PlannedWarpTransition? warpTransition)
    {
        routeDescription = new SchedulePathDescription(new Stack<Point>(), target.FacingDirection, null, null, target.LocationName, target.Tile);
        fallbackDescription = string.Empty;
        warpTransition = null;

        if (worker.currentLocation is null)
        {
            return false;
        }

        GameLocation currentLocation = worker.currentLocation;
        IReadOnlyList<string[]> locationRoutes = this.GetFallbackLocationRoutes(
            currentLocation.NameOrUniqueName,
            target.LocationName,
            avoidFirstHopLocation);
        foreach (string[] locationRoute in locationRoutes)
        {
            string nextLocationName = locationRoute[1];
            if (!this.TryBuildBestWarpLeg(worker, currentLocation, nextLocationName,
                    out Stack<Point>? route, out Point warpPoint, out Point warpTarget, out Point activationTile))
            {
                this.monitor.Log(
                    $"Location route {string.Join(" -> ", locationRoute)} rejected: no reachable {currentLocation.NameOrUniqueName} -> {nextLocationName} warp lane from {worker.TilePoint}.",
                    LogLevel.Trace);
                continue;
            }

            routeDescription = new SchedulePathDescription(route, target.FacingDirection, null, null, nextLocationName, warpTarget);
            warpTransition = new PlannedWarpTransition
            {
                TargetLocationName = nextLocationName,
                TargetTile = warpTarget,
                ActivationTile = activationTile,
                WarpTile = warpPoint,
            };
            fallbackDescription = string.Join(" -> ", locationRoute);
            this.monitor.Log(
                $"Selected location route {fallbackDescription}; current collision-aware leg is {currentLocation.NameOrUniqueName} -> {nextLocationName}.",
                LogLevel.Trace);
            return true;
        }

        return false;
    }

    private IReadOnlyList<string[]> GetFallbackLocationRoutes(
        string startLocationName,
        string targetLocationName,
        string? avoidFirstHopLocation)
    {
        Dictionary<string, string[]> links = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FarmHouse"] = new[] { "Farm" },
            ["Farm"] = new[] { "FarmHouse", "Forest", "BusStop", "Backwoods" },
            ["BusStop"] = new[] { "Farm", "Forest", "Town" },
            ["Backwoods"] = new[] { "Farm", "Mountain" },
            ["Town"] = new[] { "BusStop", "Forest", "Mountain", "Beach" },
            ["Forest"] = new[] { "Farm", "BusStop", "Town", "Woods" },
            ["Mountain"] = new[] { "Town", "Backwoods", "Railroad" },
            ["Beach"] = new[] { "Town" },
            ["Railroad"] = new[] { "Mountain" },
            ["Woods"] = new[] { "Forest" },
        };

        if (!links.ContainsKey(startLocationName) || !links.ContainsKey(targetLocationName))
            return Array.Empty<string[]>();

        List<string[]> routes = new();
        Queue<List<string>> pending = new();
        pending.Enqueue(new List<string> { startLocationName });
        while (pending.Count > 0)
        {
            List<string> path = pending.Dequeue();
            string current = path[^1];
            if (string.Equals(current, targetLocationName, StringComparison.OrdinalIgnoreCase))
            {
                routes.Add(path.ToArray());
                continue;
            }

            foreach (string next in links[current])
            {
                if ((path.Count == 1 && string.Equals(next, avoidFirstHopLocation, StringComparison.OrdinalIgnoreCase))
                    || path.Contains(next, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                List<string> extension = new(path) { next };
                pending.Enqueue(extension);
            }
        }

        return routes
            .OrderBy(route => route.Length)
            .ThenBy(route => string.Join("/", route), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryBuildBestWarpLeg(
        NPC worker,
        GameLocation currentLocation,
        string nextLocationName,
        out Stack<Point>? route,
        out Point selectedWarpPoint,
        out Point selectedWarpTarget,
        out Point selectedActivationTile)
    {
        route = null;
        selectedWarpPoint = Point.Zero;
        selectedWarpTarget = Point.Zero;
        selectedActivationTile = Point.Zero;
        HashSet<(Point WarpPoint, Point WarpTarget)> transitions = new();
        foreach (Warp warp in currentLocation.warps)
        {
            if (string.Equals(warp.TargetName, nextLocationName, StringComparison.OrdinalIgnoreCase))
                transitions.Add((new Point(warp.X, warp.Y), new Point(warp.TargetX, warp.TargetY)));
        }

        Point fallbackWarp = currentLocation.getWarpPointTo(nextLocationName, worker);
        if (fallbackWarp != Point.Zero
            && this.TryResolveWarpTarget(currentLocation, nextLocationName, fallbackWarp, worker, out Point fallbackTarget))
        {
            transitions.Add((fallbackWarp, fallbackTarget));
        }

        foreach ((Point warpPoint, Point warpTarget) in transitions)
        {
            if (!this.TryBuildCollisionAwareWarpLeg(worker, currentLocation, warpPoint, out Stack<Point>? candidateRoute, out Point activationTile)
                || candidateRoute is null
                || (route is not null && candidateRoute.Count >= route.Count))
            {
                continue;
            }

            route = candidateRoute;
            selectedWarpPoint = warpPoint;
            selectedWarpTarget = warpTarget;
            selectedActivationTile = activationTile;
        }

        if (route is null)
            return false;

        this.monitor.Log(
            $"Selected warp lane: {currentLocation.NameOrUniqueName} -> {nextLocationName}; warp={selectedWarpPoint}, "
            + $"activation={selectedActivationTile}, arrival={selectedWarpTarget}, route steps={route.Count}, candidates={transitions.Count}.",
            LogLevel.Trace);
        return true;
    }

    private bool TryBuildCollisionAwareWarpLeg(
        NPC worker,
        GameLocation currentLocation,
        Point warpPoint,
        out Stack<Point>? route,
        out Point activationTile)
    {
        route = null;
        activationTile = Point.Zero;
        List<Point> approachCandidates = new();
        if (currentLocation.isTileOnMap(warpPoint.ToVector2()))
            approachCandidates.Add(warpPoint);
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                        continue;

                    Point candidate = new(warpPoint.X + offsetX, warpPoint.Y + offsetY);
                    if (currentLocation.isTileOnMap(candidate.ToVector2()) && !approachCandidates.Contains(candidate))
                        approachCandidates.Add(candidate);
                }
            }
        }

        Stack<Point>? bestPath = null;
        Point bestApproach = Point.Zero;
        int bestTailSteps = int.MaxValue;
        int bestPathSteps = int.MaxValue;
        foreach (Point approach in approachCandidates)
        {
            Stack<Point>? candidatePath = PathFindController.findPath(
                worker.TilePoint,
                approach,
                PathFindController.isAtEndPoint,
                currentLocation,
                worker,
                30000);
            if (candidatePath is null || candidatePath.Count == 0)
                continue;

            int tailSteps = Math.Abs(warpPoint.X - approach.X) + Math.Abs(warpPoint.Y - approach.Y);
            if (tailSteps > bestTailSteps || (tailSteps == bestTailSteps && candidatePath.Count >= bestPathSteps))
                continue;

            bestPath = candidatePath;
            bestApproach = approach;
            bestTailSteps = tailSteps;
            bestPathSteps = candidatePath.Count;
        }

        if (bestPath is null)
            return false;

        route = bestPath;
        activationTile = bestApproach;
        this.monitor.Log(
            $"Warp lane candidate is reachable: warp={warpPoint}, approach={bestApproach}, "
            + $"path steps={bestPath.Count}, tail steps={Math.Abs(warpPoint.X - bestApproach.X) + Math.Abs(warpPoint.Y - bestApproach.Y)}.",
            LogLevel.Trace);
        return true;
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

        // Only an off-map warp endpoint needs vanilla schedule pathfinding. Never use
        // schedule routing to bypass crop trellises, fences, or placed farm objects.
        return !location.isTileOnMap(endTile.ToVector2())
            ? PathFindController.findPathForNPCSchedules(startTile, endTile, location, 30000, worker)
            : null;
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
