using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerNavigationManager
{
    private sealed class PlannedWarpTransition
    {
        public Point ActivationTile { get; init; }

        public Point WarpTile { get; init; }
    }

    private sealed class ReachableWarpLeg
    {
        public Stack<Point> Route { get; init; } = new();

        public Point WarpPoint { get; init; }

        public Point ActivationTile { get; init; }
    }

    private sealed class ActiveWorkerRoute
    {
        public WorkerNavigationTarget Target { get; init; } = new WorkerNavigationTarget(string.Empty, Point.Zero, 2);

        public WorkerNavigationTarget RequestedTarget { get; init; } = new WorkerNavigationTarget(string.Empty, Point.Zero, 2);

        public Func<Point, bool>? LandingValidator { get; init; }

        public Func<Point, bool>? StepValidator { get; init; }

        public bool RetryBlockedRoute { get; init; } = true;

        public Action<WorkerNavigationTarget>? OnWarpArrival { get; init; }

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

    public string GetLocalRouteDiagnostic(NPC worker, Point target)
    {
        if (worker.currentLocation is not GameLocation location)
            return "local route unavailable (worker location missing)";

        bool routeAvailable = this.TryGetLocalRouteLength(worker, target, out int routeLength);
        Point[] tiles =
        {
            target,
            new(target.X, target.Y - 1),
            new(target.X + 1, target.Y),
            new(target.X, target.Y + 1),
            new(target.X - 1, target.Y),
        };
        string tileEvidence = string.Join(", ", tiles.Distinct().Select(tile =>
        {
            Vector2 position = tile.ToVector2();
            int otherCharacters = location.characters.Count(character => character != worker && character.TilePoint == tile);
            int farmers = location.farmers.Count(farmer => farmer.TilePoint == tile);
            bool npcBarrier = location.isTileOnMap(position)
                && location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null;
            return $"{tile}:{this.DescribePathfindingTile(location, worker, tile)}/passable={location.isTileOnMap(position) && location.isTilePassable(position)}/NPCBarrier={npcBarrier}/otherCharacters={otherCharacters}/farmers={farmers}";
        }));
        string route = routeAvailable ? $"available ({routeLength} steps)" : "unavailable";
        return $"local route={route}; target and neighboring tile blockers=[{tileEvidence}]";
    }

    public WorkerObstacleRoutePlanner? CreateObstacleRoutePlanner(NPC worker, IEnumerable<Point> targets,
        IReadOnlySet<Point>? excludedTiles = null)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || worker.currentLocation is not GameLocation location)
            return null;

        return new WorkerObstacleRoutePlanner(worker.TilePoint, targets,
            tile => excludedTiles?.Contains(tile) == true
                ? WorkerRouteTileKind.Impassable
                : this.ClassifyObstacleRouteTile(location, worker, tile));
    }

    public bool IsSmallRouteObstacle(NPC worker, Point tile)
        => Context.IsWorldReady && Context.IsMainPlayer && worker.currentLocation is GameLocation location
            && this.ClassifyObstacleRouteTile(location, worker, tile) == WorkerRouteTileKind.SmallDebris;

    public bool IsWalkableWorkTile(GameLocation location, NPC worker, Point tile)
    {
        if (!Context.IsWorldReady || !location.isTileOnMap(tile.ToVector2()))
            return false;

        return WorkerWorkTilePolicy.CanStand(onMap: true,
            movementBlocked: this.IsPathfindingCollision(location, worker, tile),
            npcBarrier: location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null,
            occupied: this.IsOccupiedByOtherActor(location, worker, tile),
            warp: location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y));
    }

    private bool IsOccupiedByOtherActor(GameLocation location, NPC worker, Point tile)
    {
        Rectangle tileBox = new(tile.X * 64 + 1, tile.Y * 64 + 1, 62, 62);
        return location.characters.Any(character => character != worker
                && (character.TilePoint == tile || character.GetBoundingBox().Intersects(tileBox)))
            || location.farmers.Any(farmer => farmer.TilePoint == tile || farmer.GetBoundingBox().Intersects(tileBox));
    }

    public WorkerObstacleRoutePlanner? CreateWarpAccessObstaclePlanner(NPC worker)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || worker.currentLocation is not GameLocation location)
            return null;

        Point[] approaches = this.GetPotentialWarpPoints(location, worker)
            .SelectMany(warpPoint => this.GetWarpApproaches(location, warpPoint))
            .Distinct()
            .ToArray();
        return new WorkerObstacleRoutePlanner(worker.TilePoint, approaches,
            tile => this.ClassifyObstacleRouteTile(location, worker, tile));
    }

    private WorkerRouteTileKind ClassifyObstacleRouteTile(GameLocation location, NPC worker, Point tile)
    {
        Vector2 position = tile.ToVector2();
        if (!location.isTileOnMap(position)
            || location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y)
            || location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null
            || this.IsOccupiedByOtherActor(location, worker, tile))
            return WorkerRouteTileKind.Impassable;

        if (!this.IsPathfindingCollision(location, worker, tile))
            return WorkerRouteTileKind.Open;

        if (location.terrainFeatures.TryGetValue(position, out TerrainFeature? feature))
            return feature is Tree or FruitTree ? WorkerRouteTileKind.LargeObstacle : WorkerRouteTileKind.Impassable;

        if (location.resourceClumps.Any(clump => tile.X >= clump.Tile.X && tile.X < clump.Tile.X + clump.width.Value
                && tile.Y >= clump.Tile.Y && tile.Y < clump.Tile.Y + clump.height.Value))
            return WorkerRouteTileKind.LargeObstacle;

        if (!location.objects.TryGetValue(position, out StardewValley.Object? item)
            || !WorkerRouteObstacleClassifier.IsSmallLitter(item)
            || !location.isTilePassable(position)
            || location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null
            || location.characters.Any(character => character != worker && character.TilePoint == tile)
            || location.farmers.Any(farmer => farmer.TilePoint == tile))
            return WorkerRouteTileKind.Impassable;

        return WorkerRouteTileKind.SmallDebris;
    }

    public bool TryStartTravel(NPC worker, WorkerNavigationTarget target, string triggerReason,
        out WorkerNavigationTarget resolvedTarget, Func<Point, bool>? landingValidator = null,
        Action<WorkerNavigationTarget>? onWarpArrival = null, Func<Point, bool>? stepValidator = null,
        bool retryBlockedRoute = true)
    {
        resolvedTarget = target;
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
        string failureStage = "no reachable route";
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
            else if (this.TryResolveSafeLanding(destination, target.Tile, worker, landingValidator, out Point landing))
            {
                resolvedTarget = target with { Tile = landing };
                if (this.TryBuildDirectWarpRoute(worker, resolvedTarget, out SchedulePathDescription directRoute,
                        out PlannedWarpTransition directTransition))
                {
                    routeDescription = directRoute;
                    warpTransition = directTransition;
                }
                else
                    failureStage = "no reachable existing warp";
            }
            else
            {
                failureStage = "no safe destination landing";
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
                    this.LogRouteFailureDiagnostics(worker, target, planningTime.ElapsedMilliseconds, triggerReason, failureStage);
                }
                catch (Exception ex)
                {
                    this.monitor.Log($"Route diagnostic for {worker.displayName} failed: {ex.Message}", LogLevel.Warn);
                }
            }
            return false;
        }

        Point? rejectedStep = null;
        if (stepValidator is not null)
        {
            foreach (Point step in routeDescription!.route)
            {
                if (stepValidator(step))
                    continue;
                rejectedStep = step;
                break;
            }
        }
        if (rejectedStep is Point unsafeStep)
        {
            string diagnosticKey = $"{workerId}:unsafe-step:{worker.currentLocation.NameOrUniqueName}:{triggerReason}";
            if (!this.nextRouteDiagnosticTick.TryGetValue(diagnosticKey, out int nextTick) || Game1.ticks >= nextTick)
            {
                this.nextRouteDiagnosticTick[diagnosticKey] = Game1.ticks + RouteDiagnosticCooldownTicks;
                GameLocation stepLocation = worker.currentLocation == destination ? destination : worker.currentLocation;
                bool onMap = stepLocation.isTileOnMap(unsafeStep.ToVector2());
                this.monitor.Log($"{worker.displayName} rejected a {triggerReason} route at {stepLocation.NameOrUniqueName} "
                    + $"tile {unsafeStep} while heading to {target.Tile}: "
                    + (onMap
                        ? $"ground={stepLocation.hasTileAt(unsafeStep.X, unsafeStep.Y, "Back")}, "
                          + $"front={stepLocation.hasTileAt(unsafeStep.X, unsafeStep.Y, "Front")}, "
                          + $"buildings={stepLocation.hasTileAt(unsafeStep.X, unsafeStep.Y, "Buildings")}, "
                          + $"mapPassable={stepLocation.isTilePassable(unsafeStep.ToVector2())}, "
                          + $"npcBarrier={stepLocation.doesTileHaveProperty(unsafeStep.X, unsafeStep.Y, "NPCBarrier", "Back") is not null}, "
                          + $"warp={stepLocation.warps.Any(warp => warp.X == unsafeStep.X && warp.Y == unsafeStep.Y)}, "
                          + $"pathCollision={this.IsPathfindingCollision(stepLocation, worker, unsafeStep)}, "
                          + $"actorOccupied={this.IsOccupiedByOtherActor(stepLocation, worker, unsafeStep)}."
                        : "off map."), LogLevel.Trace);
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
            Target = resolvedTarget,
            RequestedTarget = target,
            LandingValidator = landingValidator,
            StepValidator = stepValidator,
            RetryBlockedRoute = retryBlockedRoute,
            OnWarpArrival = onWarpArrival,
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
                this.CompleteDirectWarp(worker, route, currentLocationName);
                continue;
            }

            bool reachedNextLocation = !string.Equals(currentLocationName, route.LegStartLocationName, StringComparison.OrdinalIgnoreCase);
            if (route.WarpTransition is not null && reachedNextLocation)
            {
                this.CompleteDirectWarp(worker, route, currentLocationName);
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
        if (previous.RetryBlockedRoute && previous.RetryCount < MaxRouteRetries
            && this.TryStartTravel(worker, previous.RequestedTarget, "replanning blocked route", out _,
                previous.LandingValidator, previous.OnWarpArrival, previous.StepValidator,
                previous.RetryBlockedRoute))
        {
            if (this.activeRoutes.TryGetValue(workerId, out ActiveWorkerRoute? replacement))
            {
                replacement.RetryCount = previous.RetryCount + 1;
            }
        }
    }

    private void CompleteDirectWarp(NPC worker, ActiveWorkerRoute route, string currentLocationName)
    {
        GameLocation? destination = Game1.getLocationFromName(route.RequestedTarget.LocationName);
        if (destination is null || !this.TryResolveSafeLanding(destination, route.RequestedTarget.Tile,
                worker, route.LandingValidator, out Point landing))
        {
            this.monitor.Log($"{worker.displayName} reached a warp, but no safe landing remained near {route.RequestedTarget.LocationName} {route.RequestedTarget.Tile}.", LogLevel.Info);
            this.StopTravel(worker);
            return;
        }

        WorkerNavigationTarget arrivedTarget = route.RequestedTarget with { Tile = landing };
        Action<WorkerNavigationTarget>? onWarpArrival = route.OnWarpArrival;
        this.StopTravel(worker);
        Game1.warpCharacter(worker, arrivedTarget.LocationName, arrivedTarget.Tile);
        onWarpArrival?.Invoke(arrivedTarget);
        this.monitor.Log(
            $"{worker.displayName} used existing warp {route.LegStartLocationName} {route.WarpTransition?.WarpTile} "
            + $"from {route.WarpTransition?.ActivationTile}; command issued in {currentLocationName}, "
            + $"direct arrival={arrivedTarget.LocationName} {arrivedTarget.Tile}.",
            LogLevel.Trace);
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

    // Workers walk to an authored exit, then use the mod's warp command to arrive at
    // their requested job/home tile. Vanilla outdoor Farm arrivals sit behind NPC barriers.
    private bool TryBuildDirectWarpRoute(NPC worker, WorkerNavigationTarget target,
        out SchedulePathDescription routeDescription, out PlannedWarpTransition transition)
    {
        routeDescription = new SchedulePathDescription(new Stack<Point>(), target.FacingDirection, null, null,
            target.LocationName, target.Tile);
        transition = new PlannedWarpTransition();
        GameLocation? source = worker.currentLocation;
        if (source is null)
            return false;

        HashSet<Point> warpPoints = this.GetPotentialWarpPoints(source, worker);

        List<WorkerWarpOption<ReachableWarpLeg>> options = new();
        foreach (Point warpPoint in warpPoints)
        {
            options.Add(new WorkerWarpOption<ReachableWarpLeg>(warpPoint, () =>
            {
                if (!this.TryBuildCollisionAwareWarpLeg(worker, source, worker.TilePoint, warpPoint,
                        out Stack<Point>? route, out Point activationTile) || route is null)
                    return null;

                return (new ReachableWarpLeg
                {
                    Route = route,
                    WarpPoint = warpPoint,
                    ActivationTile = activationTile,
                }, route.Count);
            }));
        }

        ReachableWarpLeg? nearest = WorkerDirectWarpPlanner.ChooseNearest(worker.TilePoint, options);
        if (nearest is null)
            return false;

        routeDescription = new SchedulePathDescription(nearest.Route, target.FacingDirection, null, null,
            target.LocationName, target.Tile);
        transition = new PlannedWarpTransition
        {
            ActivationTile = nearest.ActivationTile,
            WarpTile = nearest.WarpPoint,
        };
        this.monitor.Log($"Selected nearest reachable warp in {source.NameOrUniqueName}: warp={nearest.WarpPoint}, "
            + $"activation={nearest.ActivationTile}, walking steps={nearest.Route.Count}, direct arrival={target.LocationName} {target.Tile}.",
            LogLevel.Trace);
        return true;
    }

    private HashSet<Point> GetPotentialWarpPoints(GameLocation source, NPC worker)
    {
        HashSet<Point> warpPoints = new();
        foreach (Warp warp in source.warps)
        {
            if (!string.Equals(warp.TargetName, source.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
                warpPoints.Add(new Point(warp.X, warp.Y));
        }

        // Farmhouse doors are building warps and don't always appear in location.warps.
        string? doorDestination = source.NameOrUniqueName switch
        {
            "FarmHouse" => "Farm",
            "Farm" => "FarmHouse",
            _ => null,
        };
        if (doorDestination is not null)
        {
            Point door = source.getWarpPointTo(doorDestination, worker);
            if (door != Point.Zero && this.TryResolveWarpTarget(source, doorDestination, door, worker, out _))
                warpPoints.Add(door);
        }

        return warpPoints;
    }

    private bool TryResolveSafeLanding(GameLocation destination, Point requested, NPC worker,
        Func<Point, bool>? landingValidator, out Point landing)
    {
        return WorkerDirectWarpPlanner.TryChooseLanding(requested, 8, point =>
            this.IsWalkableWorkTile(destination, worker, point)
            && (landingValidator is null || landingValidator(point)), out landing);
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

        int bestSteps = int.MaxValue;
        foreach (Point approach in approachCandidates)
        {
            if (this.IsPathfindingCollision(currentLocation, worker, approach))
                continue;

            int lowerBound = Math.Abs(startTile.X - approach.X) + Math.Abs(startTile.Y - approach.Y);
            if (lowerBound > bestSteps)
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

            if (candidatePath.Count < bestSteps)
            {
                route = candidatePath;
                activationTile = approach;
                bestSteps = candidatePath.Count;
            }
        }

        return route is not null;
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

    private void LogRouteFailureDiagnostics(NPC worker, WorkerNavigationTarget target, long elapsedMilliseconds,
        string triggerReason, string failureStage)
    {
        GameLocation? location = worker.currentLocation;
        if (location is null)
            return;

        GameLocation? destination = Game1.getLocationFromName(target.LocationName);
        string stage = failureStage;
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
        for (int radius = 0; radius <= 1; radius++)
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
