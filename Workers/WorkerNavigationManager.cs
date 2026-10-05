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

    private sealed class ReachableWarpLeg
    {
        public Stack<Point> Route { get; init; } = new();

        public Point WarpPoint { get; init; }

        public Point WarpTarget { get; init; }

        public Point ActivationTile { get; init; }

        public string NextLocationName { get; init; } = string.Empty;
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
    private const int RouteDiagnosticCooldownTicks = 600;
    private readonly Dictionary<string, ActiveWorkerRoute> activeRoutes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> nextRouteDiagnosticTick = new(StringComparer.OrdinalIgnoreCase);
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
        System.Diagnostics.Stopwatch planningTime = System.Diagnostics.Stopwatch.StartNew();
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
        planningTime.Stop();
        if (!this.HasUsableRoute(routeDescription))
        {
            string diagnosticKey = $"{workerId}:{worker.currentLocation.NameOrUniqueName}:{target.LocationName}";
            if (!this.nextRouteDiagnosticTick.TryGetValue(diagnosticKey, out int nextTick) || Game1.ticks >= nextTick)
            {
                this.nextRouteDiagnosticTick[diagnosticKey] = Game1.ticks + RouteDiagnosticCooldownTicks;
                try
                {
                    this.LogRouteFailureDiagnostics(worker, target, planningTime.ElapsedMilliseconds, triggerReason);
                }
                catch (Exception ex)
                {
                    this.monitor.Log($"Route diagnostic for {worker.displayName} failed: {ex.Message}", LogLevel.Warn);
                }
            }
            return false;
        }

        if (planningTime.ElapsedMilliseconds >= 100)
        {
            this.monitor.Log(
                $"Worker {worker.displayName} [{workerId}] route planning from {worker.currentLocation.NameOrUniqueName} "
                + $"{worker.TilePoint} to {target.LocationName} {target.Tile} took {planningTime.ElapsedMilliseconds} ms "
                + $"(trigger: {triggerReason}).",
                LogLevel.Info);
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
        this.nextRouteDiagnosticTick.Clear();
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
        IReadOnlyList<string[]> locationRoutes = this.GetFallbackLocationRoutes(currentLocation.NameOrUniqueName,
            target.LocationName, avoidFirstHopLocation);
        Dictionary<(string Location, Point Start, string Next), IReadOnlyList<WorkerRouteOption<ReachableWarpLeg>>> legCache = new();
        Dictionary<(string Location, Point Start), bool> destinationCache = new();

        IReadOnlyList<WorkerRouteOption<ReachableWarpLeg>> GetLegOptions(string locationName, Point startTile, string nextLocationName)
        {
            var key = (locationName, startTile, nextLocationName);
            if (!legCache.TryGetValue(key, out IReadOnlyList<WorkerRouteOption<ReachableWarpLeg>>? options))
                legCache[key] = options = this.GetReachableWarpLegs(worker, locationName, startTile, nextLocationName);
            return options;
        }

        bool CanReachDestination(string locationName, Point startTile, Point destinationTile)
        {
            var key = (locationName, startTile);
            if (!destinationCache.TryGetValue(key, out bool reachable))
            {
                GameLocation? location = Game1.getLocationFromName(locationName);
                reachable = location is not null
                    && (startTile == destinationTile || this.FindCollisionAwarePath(startTile, destinationTile, location, worker) is { Count: > 0 });
                destinationCache[key] = reachable;
            }

            return reachable;
        }

        if (!WorkerRouteSelector.TrySelect(locationRoutes, worker.TilePoint, target.Tile,
                GetLegOptions, CanReachDestination, out ReachableWarpLeg? firstLeg, out string[]? selectedRoute)
            || firstLeg is null || selectedRoute is null)
            return false;

        routeDescription = new SchedulePathDescription(firstLeg.Route, target.FacingDirection, null, null,
            firstLeg.NextLocationName, firstLeg.WarpTarget);
        warpTransition = new PlannedWarpTransition
        {
            TargetLocationName = firstLeg.NextLocationName,
            TargetTile = firstLeg.WarpTarget,
            ActivationTile = firstLeg.ActivationTile,
            WarpTile = firstLeg.WarpPoint,
        };
        fallbackDescription = string.Join(" -> ", selectedRoute);
        this.monitor.Log(
            $"Selected location route {fallbackDescription}; first warp={firstLeg.WarpPoint}, "
            + $"activation={firstLeg.ActivationTile}, arrival={firstLeg.WarpTarget}, steps={firstLeg.Route.Count}.",
            LogLevel.Trace);
        return true;
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

    private IReadOnlyList<WorkerRouteOption<ReachableWarpLeg>> GetReachableWarpLegs(
        NPC worker, string locationName, Point startTile, string nextLocationName)
    {
        GameLocation? currentLocation = Game1.getLocationFromName(locationName);
        GameLocation? nextLocation = Game1.getLocationFromName(nextLocationName);
        if (currentLocation is null || nextLocation is null)
            return Array.Empty<WorkerRouteOption<ReachableWarpLeg>>();

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

        List<WorkerRouteOption<ReachableWarpLeg>> options = new();
        foreach ((Point warpPoint, Point warpTarget) in transitions)
        {
            if (!nextLocation.isTileOnMap(warpTarget.ToVector2())
                || !this.TryBuildCollisionAwareWarpLeg(worker, currentLocation, startTile, warpPoint,
                    out Stack<Point>? candidateRoute, out Point activationTile)
                || candidateRoute is null)
                continue;

            ReachableWarpLeg leg = new()
            {
                Route = candidateRoute,
                WarpPoint = warpPoint,
                WarpTarget = warpTarget,
                ActivationTile = activationTile,
                NextLocationName = nextLocationName,
            };
            options.Add(new WorkerRouteOption<ReachableWarpLeg>(leg, warpTarget));
        }

        return options.OrderBy(option => option.Leg.Route.Count).ToArray();
    }

    private bool TryBuildCollisionAwareWarpLeg(
        NPC worker,
        GameLocation currentLocation,
        Point startTile,
        Point warpPoint,
        out Stack<Point>? route,
        out Point activationTile)
    {
        route = null;
        activationTile = Point.Zero;
        List<Point> approachCandidates = this.GetWarpApproaches(currentLocation, warpPoint);

        Stack<Point>? bestPath = null;
        Point bestApproach = Point.Zero;
        int bestTailSteps = int.MaxValue;
        int bestPathSteps = int.MaxValue;
        foreach (Point approach in approachCandidates)
        {
            int tailSteps = Math.Abs(warpPoint.X - approach.X) + Math.Abs(warpPoint.Y - approach.Y);
            // Tail distance wins before path length, so a farther approach cannot replace the best route.
            if (tailSteps > bestTailSteps)
                continue;

            Stack<Point>? candidatePath = PathFindController.findPath(
                startTile,
                approach,
                PathFindController.isAtEndPoint,
                currentLocation,
                worker,
                30000);
            if (candidatePath is null || candidatePath.Count == 0)
                continue;

            if (tailSteps == bestTailSteps && candidatePath.Count >= bestPathSteps)
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

    private void LogRouteFailureDiagnostics(NPC worker, WorkerNavigationTarget target, long elapsedMilliseconds, string triggerReason)
    {
        GameLocation? location = worker.currentLocation;
        if (location is null)
            return;

        GameLocation? destination = Game1.getLocationFromName(target.LocationName);
        string stage;
        if (destination is null)
        {
            stage = "destination location missing";
        }
        else if (!destination.isTileOnMap(target.Tile.ToVector2()))
        {
            stage = "destination tile off map";
        }
        else if (location == destination)
        {
            stage = $"local path unavailable; target collision={this.DescribePathfindingTile(location, worker, target.Tile)}";
        }
        else
        {
            string[]? firstRoute = this.GetFallbackLocationRoutes(location.NameOrUniqueName, target.LocationName, null).FirstOrDefault();
            if (firstRoute is null)
            {
                stage = "no fallback location route; vanilla schedule path unavailable";
            }
            else
            {
                string nextLocationName = firstRoute[1];
                List<(Point WarpPoint, Point WarpTarget)> transitions = new();
                foreach (Warp warp in location.warps)
                {
                    if (string.Equals(warp.TargetName, nextLocationName, StringComparison.OrdinalIgnoreCase))
                        transitions.Add((new Point(warp.X, warp.Y), new Point(warp.TargetX, warp.TargetY)));
                }

                int explicitWarpCount = transitions.Count;
                Point fallbackWarp = location.getWarpPointTo(nextLocationName, worker);
                Point fallbackTarget = Point.Zero;
                bool fallbackResolved = fallbackWarp != Point.Zero
                    && this.TryResolveWarpTarget(location, nextLocationName, fallbackWarp, worker, out fallbackTarget);
                if (fallbackResolved)
                    transitions.Add((fallbackWarp, fallbackTarget));

                if (transitions.Count == 0)
                {
                    stage = $"no warp transition for {nextLocationName}; explicit={explicitWarpCount}, fallback={fallbackWarp}, fallback resolved={fallbackResolved}";
                }
                else
                {
                    string warpSummaries = string.Join("; ", transitions.Distinct().Take(6).Select(transition =>
                    {
                        List<Point> approaches = this.GetWarpApproaches(location, transition.WarpPoint);
                        int open = approaches.Count(point => !this.IsPathfindingCollision(location, worker, point));
                        return $"warp {transition.WarpPoint}->{transition.WarpTarget}: {open}/{approaches.Count} approaches collision-free";
                    }));
                    stage = $"no end-to-end path through {nextLocationName}; explicit={explicitWarpCount}, fallback={fallbackWarp}, fallback resolved={fallbackResolved}; first-hop options: {warpSummaries}";
                }
            }
        }

        Point start = worker.TilePoint;
        Point[] neighbors = { new(start.X, start.Y - 1), new(start.X + 1, start.Y), new(start.X, start.Y + 1), new(start.X - 1, start.Y) };
        string neighborSummary = string.Join(", ", neighbors.Select(point => $"{point}:{this.DescribePathfindingTile(location, worker, point)}"));
        this.monitor.Log(
            $"Route diagnostic: {worker.displayName} {location.NameOrUniqueName} {start} -> {target.LocationName} {target.Tile}; "
            + $"stage={stage}; planning={elapsedMilliseconds}ms; trigger={triggerReason}; neighbors=[{neighborSummary}].",
            LogLevel.Info);

        // The Farm's southern Forest arrival is a two-tile-wide lane. A blocker farther north
        // is invisible at the arrival tile itself, so inspect the lane without changing it.
        if (string.Equals(location.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase)
            && start.Y >= 60)
        {
            List<string> lane = new();
            for (int y = 64; y >= 59; y--)
            {
                for (int x = 40; x <= 42; x++)
                {
                    Point point = new(x, y);
                    lane.Add($"{x},{y}:{this.DescribePathfindingTile(location, worker, point)}");
                }
            }

            this.monitor.Log($"Farm south-lane collision probe: {string.Join(" ", lane)}.", LogLevel.Info);
        }
    }

    private List<Point> GetWarpApproaches(GameLocation location, Point warpPoint)
    {
        List<Point> approaches = new();
        for (int radius = 0; radius <= 3; radius++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                        continue;
                    Point point = new(warpPoint.X + offsetX, warpPoint.Y + offsetY);
                    if (location.isTileOnMap(point.ToVector2()))
                        approaches.Add(point);
                }
            }
        }

        return approaches;
    }

    private bool IsPathfindingCollision(GameLocation location, NPC worker, Point tile)
    {
        if (!location.isTileOnMap(tile.ToVector2()))
            return true;

        // Match PathFindController.findPath's NPC collision check exactly.
        Rectangle box = new(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        return location.isCollidingPosition(box, Game1.viewport, false, 0, false, worker, true, false, false, false);
    }

    private string DescribePathfindingTile(GameLocation location, NPC worker, Point tile)
    {
        if (!location.isTileOnMap(tile.ToVector2()))
            return "off-map";

        bool collides = this.IsPathfindingCollision(location, worker, tile);
        List<string> details = new() { collides ? "blocked" : "open" };
        if (location.objects.TryGetValue(tile.ToVector2(), out StardewValley.Object? placedObject))
            details.Add($"object={placedObject.GetType().Name}({placedObject.QualifiedItemId})");
        if (location.terrainFeatures.TryGetValue(tile.ToVector2(), out StardewValley.TerrainFeatures.TerrainFeature? terrain))
            details.Add($"terrain={terrain.GetType().Name}");
        return string.Join("/", details);
    }
}
