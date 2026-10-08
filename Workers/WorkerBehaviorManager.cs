using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerBehaviorManager
{
    private enum WorkerTravelPhase
    {
        None,
        TravellingToFarmTarget,
        ReturningToFarmhouse,
        RestingAtFarmhouse,
        CheckingHomeRoute,
        BlockedAtLocation,
    }

    private enum ReturnHomeReason
    {
        ExplicitIdle,
        WorkComplete,
    }

    private enum ForagerTargetKind
    {
        Forage,
        Tree,
        HardwoodTree,
        HardwoodClump,
        RouteDebris,
    }

    private sealed record ForagerTarget(string LocationName, Point ResourceTile, Point ApproachTile, ForagerTargetKind Kind,
        StardewValley.Object? ExpectedObject = null);

    private sealed record DropCollectionZone(
        GameLocation Location, Point Tile, HashSet<Debris> ExistingDebris, int CollectAfterTick, int ExpiresAtTick,
        string WorkerId, string WorkerName, WorkerTaskKind Assignment, ForagerTargetKind Kind,
        HashSet<Debris>? CapturedDebris = null)
    {
        public int ChestOnlyStacks { get; set; }

        public int ShippedStacks { get; set; }

        public bool StorageStartedLogged { get; set; }
    }

    private sealed class ActiveForagerAction
    {
        public ForagerTarget Target { get; init; } = new(string.Empty, Point.Zero, Point.Zero, ForagerTargetKind.Forage);

        public WorkerTaskKind Assignment { get; init; }

        public HashSet<Debris> ExistingDebris { get; init; } = new();

        public int NextSwingTick { get; set; }

        public int SwingCount { get; set; }
    }

    private sealed class ForagerSearchState
    {
        public GameLocation TargetLocation { get; init; } = null!;

        public GameLocation OriginLocation { get; init; } = null!;

        public Point OriginTile { get; init; }

        public WorkerTaskKind Assignment { get; init; }

        public List<ForagerTarget> Candidates { get; init; } = new();

        public int NextIndex { get; set; }

        public int HardwoodFailureDiagnosticCount { get; set; }

        public WorkerObstacleRoutePlanner? ObstaclePlanner { get; set; }
    }

    private sealed class ActiveObstacleClear
    {
        public GameLocation Location { get; init; } = null!;

        public Point ObstacleTile { get; init; }

        public Point ApproachTile { get; init; }

        public StardewValley.Object ExpectedObject { get; init; } = null!;

        public HashSet<Debris> CapturedDebris { get; } = new();

        public int NextSwingTick { get; set; }

        public int ToolActionCount { get; set; }

        public int MaxToolActions { get; init; }

        public int CooldownWaitStartedAtTick { get; set; } = -1;

        public int CooldownWaitLimitTicks { get; set; }

        public bool ContinueReturnHome { get; init; }

        public bool IsAssignedDebrisJob { get; init; }

        public ReturnHomeReason ReturnReason { get; init; }
    }

    private const int TravelRetryCooldownTicks = 60;
    private const int MaxReturnHomeRouteAttemptsPerUpdate = 1;
    private const int ReturnHomePlanningBudgetMilliseconds = 100;
    private const int MaxForagerRouteAttemptsPerUpdate = 4;
    private const int ForagerPlanningBudgetMilliseconds = 50;
    private const int ForagerSearchRetryTicks = 20;
    private const int ObstacleSearchNodesPerUpdate = 64;
    private const int ObstacleSearchMaxNodes = 12000;
    private const int MaxDebrisToolActions = 32;
    private const int MaxFailedWorkDebrisApproaches = 8;
    private const int FailedCropRetryTicks = 300;
    private const int ForagerSwingIntervalTicks = 24;
    private readonly Dictionary<string, WorkerTravelPhase> activePhases = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly WorkerNavigationManager navigationManager;
    private readonly WorkerShellManager workerShellManager;
    private readonly WorkerCombatManager combatManager;
    private readonly Dictionary<string, WorkerRuntimeSnapshot> snapshots = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> completedToday = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> activeTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ForagerTarget> activeForagerTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveForagerAction> activeForagerActions = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveObstacleClear> activeObstacleClears = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<DropCollectionZone> dropCollectionZones = new();
    private readonly Dictionary<string, WorkerNavigationTarget> returnTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReturnHomeReason> returnReasons = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> retryAfterTicks = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> nextReturnCandidateIndex = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ForagerSearchState> foragerSearches = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<Point>> failedWorkDebris = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<Point, int>> failedCropTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> lastCropFallbackTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkerObstacleRoutePlanner> homeRoutePlanners = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> lastDebugStates = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Point, string> lastHarvestReadinessStates = new();
    private readonly HashSet<Point> blockedHarvestTiles = new();

    public WorkerBehaviorManager(WorkerNavigationManager navigationManager, WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.navigationManager = navigationManager;
        this.workerShellManager = workerShellManager;
        this.combatManager = new WorkerCombatManager(workerShellManager, navigationManager, monitor,
            this.RecordCompletedWork);
        this.monitor = monitor;
    }

    public void HandleWorkerInitialized(NPC? worker, string triggerReason)
    {
        // Hiring and loading leave a worker at its saved farmhouse tile. Orders are the
        // only thing which should start movement; the old scripted round trip was noisy
        // and could send a newly hired worker outside before the player gave an order.
        if (worker is not null)
            this.monitor.Log($"{worker.displayName} initialized at {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}; waiting for an assignment (trigger: {triggerReason}).", LogLevel.Trace);
    }

    public void HandleConfiguredWorkersInitialized(string triggerReason)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (!Context.IsMainPlayer)
        {
            return;
        }

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
            this.HandleWorkerInitialized(worker, triggerReason);
    }

    public void HandleHostWarp() => this.combatManager.HandleHostWarp(workerId =>
        this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None)
            is not (WorkerTravelPhase.ReturningToFarmhouse or WorkerTravelPhase.RestingAtFarmhouse
                or WorkerTravelPhase.CheckingHomeRoute or WorkerTravelPhase.BlockedAtLocation));

    public void Update()
    {
        this.navigationManager.Update();

        if (!Context.IsWorldReady)
        {
            return;
        }

        if (!Context.IsMainPlayer)
        {
            return;
        }

        if (!Game1.shouldTimePass())
            return;

        this.UpdateDropCollectionZones();

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
        {
            if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
            {
                continue;
            }

            if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.CombatWorker)
                this.combatManager.EnsureHealth(worker, workerId);

            if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.CombatWorker
                && WorkerCombatPolicy.MustBeHomeNow(Game1.timeOfDay) && this.combatManager.EnsureHomeByMidnight(worker))
            {
                this.activePhases[workerId] = WorkerTravelPhase.RestingAtFarmhouse;
                this.ClearActiveJobState(workerId);
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
                    "Idle", "Home for the night", 0, null);
                continue;
            }

            WorkerTravelPhase phase = this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None);
            if (this.activeObstacleClears.TryGetValue(workerId, out ActiveObstacleClear? obstacleClear))
            {
                this.UpdateObstacleClear(worker, workerId, this.workerShellManager.GetAssignedTask(workerId), obstacleClear);
                continue;
            }

            if (phase == WorkerTravelPhase.CheckingHomeRoute)
            {
                this.UpdateHomeRouteCheck(worker, workerId);
                continue;
            }

            if (phase is WorkerTravelPhase.ReturningToFarmhouse or WorkerTravelPhase.RestingAtFarmhouse)
            {
                this.UpdateReturnHome(worker, workerId, phase);
                continue;
            }

            if (phase == WorkerTravelPhase.BlockedAtLocation)
                continue;

            WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
            if (assignment != WorkerTaskKind.Idle && this.workerShellManager.CanWorkerWorkToday(workerId))
            {
                this.UpdateAssignedWork(worker, workerId, assignment);
                continue;
            }

        }
    }

    public void Reset()
    {
        this.activePhases.Clear();
        this.snapshots.Clear();
        this.completedToday.Clear();
        this.activeTargets.Clear();
        this.activeForagerTargets.Clear();
        this.activeForagerActions.Clear();
        this.activeObstacleClears.Clear();
        this.dropCollectionZones.Clear();
        this.returnTargets.Clear();
        this.returnReasons.Clear();
        this.retryAfterTicks.Clear();
        this.nextReturnCandidateIndex.Clear();
        this.foragerSearches.Clear();
        this.failedWorkDebris.Clear();
        this.failedCropTargets.Clear();
        this.lastCropFallbackTargets.Clear();
        this.homeRoutePlanners.Clear();
        this.lastDebugStates.Clear();
        this.lastHarvestReadinessStates.Clear();
        this.blockedHarvestTiles.Clear();
        this.navigationManager.Reset();
        this.combatManager.Reset();
    }

    public bool TryAssignTask(string workerId, WorkerTaskKind task, out string message)
    {
        message = string.Empty;
        if (!Enum.IsDefined(typeof(WorkerTaskKind), task) || !this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        {
            message = $"Worker '{workerId}' was not found.";
            return false;
        }
        WorkerProfession profession = this.workerShellManager.GetWorkerProfession(workerId);
        if (!WorkerTaskPolicy.IsTaskAllowed(profession, task))
        {
            message = $"{WorkerTaskPolicy.GetProfessionLabel(profession)} workers can't perform that job.";
            return false;
        }
        if (!this.workerShellManager.TrySetAssignedTask(workerId, task))
        {
            message = "Only the host can assign worker tasks.";
            return false;
        }
        this.navigationManager.StopTravel(worker);
        this.combatManager.Stop(workerId);
        if (task == WorkerTaskKind.Idle)
            this.combatManager.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetCombatArea(workerId));
        if (!this.navigationManager.HasForeignController(worker))
        {
            this.navigationManager.StopWorker(worker);
        }
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.ClearActiveJobState(workerId);
        this.returnTargets.Remove(workerId);
        this.returnReasons.Remove(workerId);
        this.retryAfterTicks.Remove(workerId);
        this.nextReturnCandidateIndex.Remove(workerId);
        this.foragerSearches.Remove(workerId);
        this.failedWorkDebris.Remove(workerId);
        this.failedCropTargets.Remove(workerId);
        this.lastCropFallbackTargets.Remove(workerId);
        if (task == WorkerTaskKind.Idle)
        {
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        }
        else
        {
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(task, "Selecting", $"Assigned: {WorkerTaskPolicy.GetTaskLabel(task)}", 0, null);
        }
        this.LogState(workerId, worker, $"assignment changed to {task}");
        message = $"{worker.displayName} is now assigned to {WorkerTaskPolicy.GetTaskLabel(task)}.";
        return true;
    }

    public void StopWorker(string workerId)
    {
        if (this.workerShellManager.TryGetWorker(workerId, out NPC? worker) && worker is not null)
            this.navigationManager.StopWorker(worker);
        this.ClearActiveJobState(workerId);
        this.returnTargets.Remove(workerId);
        this.returnReasons.Remove(workerId);
        this.retryAfterTicks.Remove(workerId);
        this.nextReturnCandidateIndex.Remove(workerId);
        this.foragerSearches.Remove(workerId);
        this.failedWorkDebris.Remove(workerId);
        this.failedCropTargets.Remove(workerId);
        this.lastCropFallbackTargets.Remove(workerId);
        this.activePhases.Remove(workerId);
        this.completedToday.Remove(workerId);
        this.combatManager.Stop(workerId);
    }

    private void ClearActiveJobState(string workerId)
    {
        this.activeTargets.Remove(workerId);
        this.activeForagerTargets.Remove(workerId);
        this.activeForagerActions.Remove(workerId);
        this.activeObstacleClears.Remove(workerId);
        this.homeRoutePlanners.Remove(workerId);
        this.foragerSearches.Remove(workerId);
    }

    public bool TrySetForageLocation(string workerId, string locationName, out string message)
    {
        if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        {
            message = $"Worker '{workerId}' was not found.";
            return false;
        }

        if (!this.workerShellManager.TrySetForageLocation(workerId, locationName, out message))
            return false;

        this.navigationManager.StopWorker(worker);
        this.ClearActiveJobState(workerId);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        return true;
    }

    public bool TrySetCombatArea(string workerId, string area, out string message)
    {
        if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        {
            message = $"Worker '{workerId}' was not found.";
            return false;
        }
        string previousArea = this.workerShellManager.GetCombatArea(workerId);
        if (!this.workerShellManager.TrySetCombatArea(workerId, area, out message))
            return false;
        this.navigationManager.StopWorker(worker);
        this.combatManager.ExitGeneratedFloorForReturn(worker, previousArea);
        this.combatManager.Stop(workerId);
        this.ClearActiveJobState(workerId);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        return true;
    }

    public WorkerRuntimeSnapshot GetRuntimeSnapshot(string workerId)
    {
        WorkerRuntimeSnapshot current = this.snapshots.TryGetValue(workerId, out WorkerRuntimeSnapshot snapshot)
            ? snapshot
            : new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId), "Idle", "Waiting for an assignment", 0, null);
        return current with { CompletedToday = this.completedToday.GetValueOrDefault(workerId) };
    }

    public bool TryGetCombatHealth(string workerId, out int health, out int maxHealth)
        => this.combatManager.TryGetHealth(workerId, out health, out maxHealth);

    public void DrawCombatHealthBars(Microsoft.Xna.Framework.Graphics.SpriteBatch batch)
        => this.combatManager.DrawHealthBars(batch);

    private void RecordCompletedWork(string workerId)
    {
        this.completedToday[workerId] = this.completedToday.GetValueOrDefault(workerId) + 1;
    }

    // Vanilla object and tree tools need a Farmer as lastUser for drops. Scope
    // their calls so gainExperience cannot change that farmer's skills or mastery.
    private static T WithoutFarmerExperience<T>(Func<T> action)
    {
        using IDisposable scope = WorkerExperienceSuppression.BeginScope();
        return action();
    }

    private static void WithoutFarmerExperience(Action action)
        => WithoutFarmerExperience(() => { action(); return true; });

    private void UpdateReturnHome(NPC worker, string workerId, WorkerTravelPhase phase)
    {
        if (phase == WorkerTravelPhase.RestingAtFarmhouse)
        {
            return;
        }

        if (!this.returnTargets.TryGetValue(workerId, out WorkerNavigationTarget? homeTarget) || homeTarget is null)
        {
            if (Game1.shouldTimePass() && this.CanRetryTravel(workerId))
            {
                this.BeginReturnHome(worker, workerId, this.returnReasons.GetValueOrDefault(workerId, ReturnHomeReason.WorkComplete));
            }

            return;
        }

        if (worker.currentLocation?.NameOrUniqueName == homeTarget.LocationName
            && worker.TilePoint == homeTarget.Tile
            && worker.controller is null)
        {
            this.navigationManager.ApplyIdlePose(worker);
            this.activePhases[workerId] = WorkerTravelPhase.RestingAtFarmhouse;
            this.retryAfterTicks.Remove(workerId);
            this.nextReturnCandidateIndex.Remove(workerId);
            WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
            string status = this.returnReasons.GetValueOrDefault(workerId, ReturnHomeReason.WorkComplete) == ReturnHomeReason.ExplicitIdle
                ? "Idle at home"
                : "Work complete; resting at home";
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Idle", status, 0, null);
            this.LogState(workerId, worker, status);
            return;
        }

        if (this.navigationManager.HasActiveRoute(workerId)
            || worker.controller is not null
            || !this.CanRetryTravel(workerId))
        {
            return;
        }

        if (!Game1.shouldTimePass())
        {
            return;
        }

        HashSet<Point> reserved = this.returnTargets
            .Where(pair => !string.Equals(pair.Key, workerId, System.StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Tile)
            .ToHashSet();
        HashSet<Point> allowed = this.workerShellManager.GetWorkerReturnTargets(worker, reserved)
            .Select(candidate => candidate.Tile)
            .ToHashSet();
        this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
        if (!this.navigationManager.TryStartTravel(worker, homeTarget, "retry return home", out WorkerNavigationTarget resolvedTarget,
                allowed.Contains, actual => this.returnTargets[workerId] = actual))
        {
            this.returnTargets.Remove(workerId);
            this.BeginHomeRouteCheck(worker, workerId, this.returnReasons.GetValueOrDefault(workerId, ReturnHomeReason.WorkComplete));
        }
        else
            this.returnTargets[workerId] = resolvedTarget;
    }

    private void BeginReturnHome(NPC worker, string workerId, ReturnHomeReason reason)
    {
        if (!this.activePhases.TryGetValue(workerId, out WorkerTravelPhase previousPhase)
            || previousPhase != WorkerTravelPhase.ReturningToFarmhouse)
            this.nextReturnCandidateIndex.Remove(workerId);

        this.ClearActiveJobState(workerId);
        this.navigationManager.StopTravel(worker);
        this.returnTargets.Remove(workerId);
        this.returnReasons[workerId] = reason;
        this.activePhases[workerId] = WorkerTravelPhase.ReturningToFarmhouse;
        this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;

        HashSet<Point> reservedIdleTargets = this.returnTargets
            .Where(pair => !string.Equals(pair.Key, workerId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Tile)
            .ToHashSet();
        IReadOnlyList<WorkerNavigationTarget> candidates = this.workerShellManager.GetWorkerReturnTargets(worker, reservedIdleTargets);
        HashSet<Point> allowedLandingTiles = candidates.Select(candidate => candidate.Tile).ToHashSet();
        int startIndex = candidates.Count > 0 ? this.nextReturnCandidateIndex.GetValueOrDefault(workerId) % candidates.Count : 0;
        int attempted = 0;
        System.Diagnostics.Stopwatch planningTime = System.Diagnostics.Stopwatch.StartNew();
        while (attempted < Math.Min(candidates.Count, MaxReturnHomeRouteAttemptsPerUpdate)
            && (attempted == 0 || planningTime.ElapsedMilliseconds < ReturnHomePlanningBudgetMilliseconds))
        {
            WorkerNavigationTarget candidate = candidates[(startIndex + attempted) % candidates.Count];
            attempted++;
            if (!this.navigationManager.TryStartTravel(worker, candidate,
                    reason == ReturnHomeReason.ExplicitIdle ? "idle/return home" : "work complete/return home",
                    out WorkerNavigationTarget resolvedTarget, allowedLandingTiles.Contains,
                    actual => this.returnTargets[workerId] = actual))
            {
                continue;
            }

            this.returnTargets[workerId] = resolvedTarget;
            this.retryAfterTicks.Remove(workerId);
            this.nextReturnCandidateIndex.Remove(workerId);
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(
                this.workerShellManager.GetAssignedTask(workerId),
                "Traveling",
                "Returning home",
                0,
                resolvedTarget.Tile);
            this.LogState(workerId, worker, $"returning home to {resolvedTarget.LocationName} tile {resolvedTarget.Tile}");
            return;
        }

        if (candidates.Count > 0)
            this.nextReturnCandidateIndex[workerId] = (startIndex + attempted) % candidates.Count;
        this.BeginHomeRouteCheck(worker, workerId, reason);
    }

    private void BeginHomeRouteCheck(NPC worker, string workerId, ReturnHomeReason reason)
    {
        this.returnReasons[workerId] = reason;
        this.returnTargets.Remove(workerId);
        this.retryAfterTicks.Remove(workerId);
        WorkerObstacleRoutePlanner? planner = this.navigationManager.CreateWarpAccessObstaclePlanner(worker);
        if (planner is null)
        {
            this.DeferEmergencyReturnCheck(worker, workerId, "home exit route planner unavailable");
            return;
        }

        this.homeRoutePlanners[workerId] = planner;
        this.activePhases[workerId] = WorkerTravelPhase.CheckingHomeRoute;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
            "Checking", "Checking clear paths to exits", 0, null);
        this.LogState(workerId, worker, "checking for reachable exits and small debris that can be cleared");
    }

    private void UpdateHomeRouteCheck(NPC worker, string workerId)
    {
        if (worker.controller is not null || this.navigationManager.HasActiveRoute(workerId)
            || !Game1.shouldTimePass() || !this.homeRoutePlanners.TryGetValue(workerId, out WorkerObstacleRoutePlanner? planner))
            return;

        WorkerObstaclePlanStatus status = planner.Advance(ObstacleSearchNodesPerUpdate, ObstacleSearchMaxNodes,
            out WorkerObstacleClearance? clearance);
        if (status == WorkerObstaclePlanStatus.Searching)
            return;

        if (status == WorkerObstaclePlanStatus.ClearSmallDebris && clearance is not null
            && worker.currentLocation is GameLocation location
            && location.objects.TryGetValue(clearance.ObstacleTile.ToVector2(), out StardewValley.Object? item)
            && this.navigationManager.IsSmallRouteObstacle(worker, clearance.ObstacleTile))
        {
            WorkerNavigationTarget approach = new(location.NameOrUniqueName, clearance.ApproachTile, TestWorkerDefinition.FacingDirection);
            if (this.navigationManager.TryStartTravel(worker, approach, "walk to clear route to exit", out _))
            {
                ReturnHomeReason reason = this.returnReasons.GetValueOrDefault(workerId, ReturnHomeReason.WorkComplete);
                this.activeObstacleClears[workerId] = new ActiveObstacleClear
                {
                    Location = location,
                    ObstacleTile = clearance.ObstacleTile,
                    ApproachTile = clearance.ApproachTile,
                    ExpectedObject = item,
                    NextSwingTick = Game1.ticks,
                    MaxToolActions = WorkerObstacleToolProgressPolicy.GetMaximumActions(
                        item.MinutesUntilReady, damagePerToolAction: 1, hardLimit: MaxDebrisToolActions),
                    ContinueReturnHome = true,
                    ReturnReason = reason,
                };
                this.homeRoutePlanners.Remove(workerId);
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
                    "Clearing", "Clearing small debris from the exit route", 0, clearance.ObstacleTile);
                this.monitor.Log($"{worker.displayName} will clear small route debris at {clearance.ObstacleTile} from {clearance.ApproachTile} to reach an exit in {location.NameOrUniqueName}.", LogLevel.Info);
                return;
            }

            this.homeRoutePlanners.Remove(workerId);
            this.DeferEmergencyReturnCheck(worker, workerId,
                "could not walk to small exit-route debris despite a clear route plan; retrying visible return");
            return;
        }

        if (status == WorkerObstaclePlanStatus.ReachableClearRoute)
        {
            // The route search found a clear walk to an authored exit. Keep trying
            // the ordinary visible return path; an emergency warp is not justified.
            this.homeRoutePlanners.Remove(workerId);
            this.DeferEmergencyReturnCheck(worker, workerId, "clear authored exit route found; delaying emergency placement and retrying visible return");
            return;
        }

        if (status == WorkerObstaclePlanStatus.SearchLimitReached)
        {
            this.homeRoutePlanners.Remove(workerId);
            this.DeferEmergencyReturnCheck(worker, workerId, "exit search limit reached without proof of no route; retrying before emergency placement");
            return;
        }

        this.homeRoutePlanners.Remove(workerId);
        this.TryEmergencyReturnHome(worker, workerId,
            this.returnReasons.GetValueOrDefault(workerId, ReturnHomeReason.WorkComplete),
            $"no walkable exit route after obstacle search ({status})");
    }

    private void DeferEmergencyReturnCheck(NPC worker, string workerId, string reason)
    {
        this.activePhases[workerId] = WorkerTravelPhase.ReturningToFarmhouse;
        this.returnTargets.Remove(workerId);
        this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
            "Checking", "Retrying visible return before emergency placement", 0, null);
        this.LogState(workerId, worker, reason);
    }

    private void TryEmergencyReturnHome(NPC worker, string workerId, ReturnHomeReason reason, string trigger)
    {
        HashSet<Point> reserved = this.returnTargets
            .Where(pair => !string.Equals(pair.Key, workerId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Tile)
            .ToHashSet();
        IReadOnlyList<WorkerNavigationTarget> safeTargets = this.workerShellManager.GetWorkerReturnTargets(worker, reserved);
        bool safeLandingAvailable = safeTargets.Count > 0;
        if (WorkerReturnFallbackPolicy.MayUseEmergencyWarp(
                visibleRouteAvailable: false, clearableDebrisRouteAvailable: false, safeLandingAvailable: safeLandingAvailable)
            && !this.navigationManager.HasForeignController(worker))
        {
            WorkerNavigationTarget homeTarget = safeTargets[0];
            this.navigationManager.StopWorker(worker);
            try
            {
                Game1.warpCharacter(worker, homeTarget.LocationName, homeTarget.Tile);
                this.returnTargets[workerId] = homeTarget;
                this.returnReasons[workerId] = reason;
                this.activePhases[workerId] = WorkerTravelPhase.ReturningToFarmhouse;
                this.retryAfterTicks.Remove(workerId);
                this.homeRoutePlanners.Remove(workerId);
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
                    "Traveling", "Emergency return home", 0, homeTarget.Tile);
                this.navigationManager.ApplyIdlePose(worker);
                this.monitor.Log($"{worker.displayName} used the last-resort home warp after no walkable exit route remained ({trigger}); safe FarmHouse landing={homeTarget.Tile}.", LogLevel.Warn);
                return;
            }
            catch (Exception ex)
            {
                this.monitor.Log($"{worker.displayName} emergency home warp failed: {ex.Message}", LogLevel.Warn);
            }
        }

        string blockedLocation = worker.currentLocation?.NameOrUniqueName ?? TestWorkerDefinition.LocationName;
        this.workerShellManager.ReportBlockedRoute(workerId, this.workerShellManager.GetAssignedTask(workerId), blockedLocation);
        this.navigationManager.ApplyIdlePose(worker);
        this.returnTargets.Remove(workerId);
        this.returnReasons[workerId] = reason;
        this.activePhases[workerId] = WorkerTravelPhase.BlockedAtLocation;
        this.retryAfterTicks.Remove(workerId);
        this.homeRoutePlanners.Remove(workerId);
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
            "Blocked", "No safe way home; waiting for a new order", 0, null);
        this.LogState(workerId, worker, $"emergency home placement unavailable; blocked ({trigger})");
    }

    private bool CanRetryTravel(string workerId)
    {
        return !this.retryAfterTicks.TryGetValue(workerId, out int retryAfter) || Game1.ticks >= retryAfter;
    }

    private void UpdateAssignedWork(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (assignment == WorkerTaskKind.SlayMonsters)
        {
            if (this.combatManager.Update(worker, workerId, out WorkerRuntimeSnapshot combatSnapshot))
            {
                this.combatManager.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetCombatArea(workerId));
                this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            }
            else
                this.snapshots[workerId] = combatSnapshot;
            return;
        }
        if (assignment == WorkerTaskKind.ClearDebris
            || this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Forager)
        {
            this.UpdateForagerWork(worker, workerId, assignment);
            return;
        }

        if (!WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay) || worker.controller is not null)
            return;
        Farm farm = Game1.getFarm();
        if (worker.currentLocation == farm
            && this.activeTargets.TryGetValue(workerId, out Point target)
            && WorkerCropApproachPolicy.IsInWorkRange(worker.TilePoint, target)
            && worker.controller is null && !this.navigationManager.HasActiveRoute(workerId))
        {
            this.navigationManager.ApplyIdlePose(worker);
            WorkerTaskKind action = WorkerTaskKind.Idle;
            if (farm.terrainFeatures.TryGetValue(target.ToVector2(), out TerrainFeature? feature)
                && feature is HoeDirt dirt
                && dirt.crop is not null
                && !dirt.crop.dead.Value)
            {
                action = WorkerTaskPolicy.SelectCropAction(
                    assignment,
                    alive: true,
                    harvestable: IsHarvestable(dirt.crop),
                    needsWater: !dirt.isWatered());
            }

            this.monitor.Log(
                $"{worker.displayName} arrived at crop tile {target}; assignment={assignment}, resolved action={action}.",
                LogLevel.Trace);
            if (this.PerformJobAt(workerId, farm, target, action))
                this.RecordCompletedWork(workerId);
            this.activeTargets.Remove(workerId);
            this.LogState(workerId, worker, $"finished action at target tile {target}; rescanning farm");
        }

        if (this.activeTargets.ContainsKey(workerId))
        {
            if (worker.controller is null
                && !this.navigationManager.HasActiveRoute(workerId)
                && Game1.shouldTimePass())
            {
                this.MarkFailedCropTarget(workerId, this.activeTargets[workerId]);
                this.activeTargets.Remove(workerId);
                this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
            }

            if (this.activeTargets.ContainsKey(workerId))
                return;
        }

        if (assignment is WorkerTaskKind.HarvestCrops or WorkerTaskKind.TendCrops)
        {
            if (this.TryFindCropTarget(worker, workerId, farm, assignment, WorkerTaskKind.HarvestCrops, out Point harvestTarget))
            {
                this.StartJobTravel(worker, workerId, harvestTarget, WorkerTaskKind.HarvestCrops, "travel to harvest target");
                return;
            }

            this.LogHarvestReadinessSamples(farm);
        }

        if (assignment is WorkerTaskKind.WaterCrops or WorkerTaskKind.TendCrops
            && this.TryFindCropTarget(worker, workerId, farm, assignment, WorkerTaskKind.WaterCrops, out Point waterTarget))
        {
            this.StartJobTravel(worker, workerId, waterTarget, WorkerTaskKind.WaterCrops, "travel to watering target");
            return;
        }

        this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
    }

    private void UpdateForagerWork(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (!WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
        {
            if (Game1.shouldTimePass() && worker.controller is null && !this.navigationManager.HasActiveRoute(workerId))
                this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        if (!Game1.shouldTimePass())
            return;

        if (this.activeForagerActions.TryGetValue(workerId, out ActiveForagerAction? action))
        {
            this.UpdateForagerAction(worker, workerId, action);
            return;
        }

        if (this.activeObstacleClears.TryGetValue(workerId, out ActiveObstacleClear? obstacleClear))
        {
            this.UpdateObstacleClear(worker, workerId, assignment, obstacleClear);
            return;
        }

        if (worker.controller is not null)
            return;

        string locationName = assignment == WorkerTaskKind.ClearDebris
            && this.workerShellManager.GetWorkerProfession(workerId) != WorkerProfession.Forager
            ? "Farm"
            : this.workerShellManager.GetForageLocationName(workerId);
        GameLocation? location = Game1.getLocationFromName(locationName);
        if (location is null || !WorkerForageAreaCatalog.IsValidLocation(locationName))
        {
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        if (this.activeForagerTargets.TryGetValue(workerId, out ForagerTarget? active))
        {
            if (worker.currentLocation?.NameOrUniqueName == active.LocationName
                && worker.TilePoint == active.ApproachTile
                && worker.controller is null)
            {
                this.BeginForagerAction(worker, workerId, location, active, assignment);
                return;
            }
            else if (worker.controller is null && !this.navigationManager.HasActiveRoute(workerId) && Game1.shouldTimePass())
            {
                this.activeForagerTargets.Remove(workerId);
                this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
            }

            if (this.activeForagerTargets.ContainsKey(workerId))
                return;
        }

        if (!this.CanRetryTravel(workerId))
            return;

        if (!this.foragerSearches.TryGetValue(workerId, out ForagerSearchState? search)
            || search.TargetLocation != location
            || search.OriginLocation != worker.currentLocation
            || search.OriginTile != worker.TilePoint
            || search.Assignment != assignment)
        {
            // Build one search snapshot after each completed job. Route attempts are
            // spread across updates so an obstructed field cannot block a frame.
            List<ForagerTarget> candidates = this.GetForagerTargets(worker, workerId, location, assignment).ToList();
            if (candidates.Count == 0)
            {
                this.foragerSearches.Remove(workerId);
                if (this.HasFallingTree(location, assignment))
                {
                    this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Working", "Waiting for a felled tree to settle", 0, null);
                    return;
                }

                ForagerSearchState emptySearch = new()
                {
                    TargetLocation = location,
                    OriginLocation = worker.currentLocation!,
                    OriginTile = worker.TilePoint,
                    Assignment = assignment,
                };
                if (this.TryStartObstaclePlan(worker, workerId, assignment, emptySearch))
                    this.foragerSearches[workerId] = emptySearch;
                else
                    this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
                return;
            }

            search = new ForagerSearchState
            {
                TargetLocation = location,
                OriginLocation = worker.currentLocation!,
                OriginTile = worker.TilePoint,
                Assignment = assignment,
                Candidates = worker.currentLocation == location
                    ? candidates.OrderBy(target => Math.Abs(worker.TilePoint.X - target.ApproachTile.X)
                        + Math.Abs(worker.TilePoint.Y - target.ApproachTile.Y)).ToList()
                    : candidates.OrderBy(target => target.ResourceTile.X + target.ResourceTile.Y).ToList(),
            };
            this.foragerSearches[workerId] = search;
        }

        if (search.ObstaclePlanner is not null)
        {
            this.UpdateObstaclePlan(worker, workerId, assignment, search);
            return;
        }

        WorkerNavigationTarget? resolvedTarget = null;
        bool TryCandidate(ForagerTarget target)
        {
            if (assignment == WorkerTaskKind.ClearDebris
                && (this.IsDebrisReservedByAnother(workerId, search.TargetLocation, target.ResourceTile)
                    || !search.TargetLocation.objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? current)
                    || !ReferenceEquals(current, target.ExpectedObject)
                    || !WorkerRouteObstacleClassifier.IsSmallLitter(current)))
                return false;

            WorkerNavigationTarget navigationTarget = new(target.LocationName, target.ApproachTile, TestWorkerDefinition.FacingDirection);
            HashSet<Point> validApproaches = search.Candidates
                .Where(candidate => candidate.ResourceTile == target.ResourceTile && candidate.Kind == target.Kind)
                .Select(candidate => candidate.ApproachTile)
                .ToHashSet();
            if (!this.navigationManager.TryStartTravel(worker, navigationTarget,
                    $"travel to {WorkerTaskPolicy.GetTaskLabel(assignment).ToLowerInvariant()} target",
                    out WorkerNavigationTarget candidateTarget, validApproaches.Contains,
                    actual => this.activeForagerTargets[workerId] = target with { ApproachTile = actual.Tile }))
            {
                if (assignment == WorkerTaskKind.ChopHardwood && worker.currentLocation == search.TargetLocation)
                    this.LogFailedHardwoodCandidate(worker, search, target);
                return false;
            }

            resolvedTarget = candidateTarget;
            return true;
        }

        bool foundRoute = WorkerBoundedCandidateSearch.TryFind(search.Candidates, search.NextIndex,
            MaxForagerRouteAttemptsPerUpdate, ForagerPlanningBudgetMilliseconds, TryCandidate,
            out ForagerTarget? selectedTarget, out int nextIndex, out int attempted,
            out bool exhausted, out long planningMilliseconds);
        if (planningMilliseconds >= 100)
            this.monitor.Log($"Forager route search took {planningMilliseconds} ms for {attempted} candidates in {location.NameOrUniqueName}.", LogLevel.Info);
        if (foundRoute && selectedTarget is not null && resolvedTarget is not null)
        {
            ForagerTarget target = selectedTarget;

            this.activeForagerTargets[workerId] = target with { ApproachTile = resolvedTarget.Tile };
            this.foragerSearches.Remove(workerId);
            this.activePhases[workerId] = WorkerTravelPhase.TravellingToFarmTarget;
            this.retryAfterTicks.Remove(workerId);
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Traveling",
                $"Walking to {WorkerForageAreaCatalog.GetDisplayName(locationName)} {target.ResourceTile}", 0, target.ResourceTile);
            this.LogState(workerId, worker, $"selected {assignment} target {locationName} {target.ResourceTile} via {target.ApproachTile}");
            return;
        }

        if (exhausted)
        {
            if (!this.TryStartObstaclePlan(worker, workerId, assignment, search))
                this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        search.NextIndex = nextIndex;
        this.retryAfterTicks[workerId] = Game1.ticks + ForagerSearchRetryTicks;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Blocked", "No reachable target found; retrying", 0, null);
    }

    private bool TryStartObstaclePlan(NPC worker, string workerId, WorkerTaskKind assignment, ForagerSearchState search)
    {
        if (worker.currentLocation != search.TargetLocation)
            return false;

        Point[] potentialApproaches = this.GetForagerTargets(worker, workerId, search.TargetLocation, assignment, includeBlockedApproaches: true)
            .Select(target => target.ApproachTile)
            .Distinct()
            .ToArray();
        if (potentialApproaches.Length == 0)
            return false;

        this.failedWorkDebris.TryGetValue(workerId, out HashSet<Point>? excludedTiles);
        search.ObstaclePlanner = this.navigationManager.CreateObstacleRoutePlanner(worker, potentialApproaches, excludedTiles);
        if (search.ObstaclePlanner is null)
            return false;

        this.retryAfterTicks.Remove(workerId);
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Checking", "Checking blocked routes", 0, null);
        return true;
    }

    private void UpdateObstaclePlan(NPC worker, string workerId, WorkerTaskKind assignment, ForagerSearchState search)
    {
        WorkerObstaclePlanStatus status = search.ObstaclePlanner!.Advance(
            ObstacleSearchNodesPerUpdate, ObstacleSearchMaxNodes, out WorkerObstacleClearance? clearance);
        if (status == WorkerObstaclePlanStatus.Searching)
            return;

        if (status == WorkerObstaclePlanStatus.ClearSmallDebris && clearance is not null
            && search.TargetLocation.objects.TryGetValue(clearance.ObstacleTile.ToVector2(), out StardewValley.Object? item)
            && (assignment != WorkerTaskKind.ClearDebris
                || !this.IsDebrisReservedByAnother(workerId, search.TargetLocation, clearance.ObstacleTile))
            && this.navigationManager.IsSmallRouteObstacle(worker, clearance.ObstacleTile))
        {
            WorkerNavigationTarget approach = new(search.TargetLocation.NameOrUniqueName,
                clearance.ApproachTile, TestWorkerDefinition.FacingDirection);
            if (this.navigationManager.TryStartTravel(worker, approach, "walk to route debris", out _))
            {
                this.activeObstacleClears[workerId] = new ActiveObstacleClear
                {
                    Location = search.TargetLocation,
                    ObstacleTile = clearance.ObstacleTile,
                    ApproachTile = clearance.ApproachTile,
                    ExpectedObject = item,
                    NextSwingTick = Game1.ticks,
                    MaxToolActions = WorkerObstacleToolProgressPolicy.GetMaximumActions(
                        item.MinutesUntilReady, damagePerToolAction: 1, hardLimit: MaxDebrisToolActions),
                    IsAssignedDebrisJob = assignment == WorkerTaskKind.ClearDebris,
                };
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Clearing", "Clearing small route debris", 0, clearance.ObstacleTile);
                this.monitor.Log($"{worker.displayName} will clear small route debris at {clearance.ObstacleTile} from {clearance.ApproachTile} in {search.TargetLocation.NameOrUniqueName}.", LogLevel.Info);
                return;
            }
        }

        // A live approach can become usable after debris clearing, or may have been
        // absent from the initial snapshot. Resource eligibility and NPC walkability
        // are rechecked together; snapshot membership is diagnostic only.
        List<ForagerTarget> stillEligibleTargets = this.GetForagerTargets(worker, workerId, search.TargetLocation, assignment).ToList();
        if (status == WorkerObstaclePlanStatus.ReachableClearRoute)
        {
            Point? reached = search.ObstaclePlanner.ReachedTarget;
            bool matchedSnapshot = reached is Point point
                && search.Candidates.Any(candidate => candidate.ApproachTile == point);
            List<ForagerTarget> eligibleMatches = reached is Point reachedPoint
                ? stillEligibleTargets.Where(candidate => candidate.ApproachTile == reachedPoint).ToList()
                : new List<ForagerTarget>();
            string matchedResources = eligibleMatches.Count == 0
                ? "none"
                : string.Join(", ", eligibleMatches.Select(candidate => $"{candidate.Kind}@{candidate.ResourceTile}"));
            this.monitor.Log($"Forager fallback route result: reached approach={reached?.ToString() ?? "unknown"}; "
                + $"search-snapshot match={matchedSnapshot}; currently eligible match={eligibleMatches.Count > 0}; "
                + $"eligible resources=[{matchedResources}].", LogLevel.Info);
        }

        if (status == WorkerObstaclePlanStatus.ReachableClearRoute
            && search.ObstaclePlanner.ReachedTarget is Point reachableApproach
            && WorkerForagerApproachMatcher.TryFind(stillEligibleTargets, reachableApproach,
                target => target.ApproachTile, out ForagerTarget? currentCandidate)
            && currentCandidate is not null)
        {
            HashSet<Point> validApproaches = stillEligibleTargets
                .Where(candidate => candidate.ResourceTile == currentCandidate.ResourceTile && candidate.Kind == currentCandidate.Kind)
                .Select(candidate => candidate.ApproachTile)
                .ToHashSet();
            WorkerNavigationTarget navigationTarget = new(currentCandidate.LocationName,
                currentCandidate.ApproachTile, TestWorkerDefinition.FacingDirection);
            if (this.navigationManager.TryStartTravel(worker, navigationTarget,
                    $"retry visible route to reachable {WorkerTaskPolicy.GetTaskLabel(assignment).ToLowerInvariant()} target",
                    out WorkerNavigationTarget resolvedTarget, validApproaches.Contains,
                    actual => this.activeForagerTargets[workerId] = currentCandidate with { ApproachTile = actual.Tile }))
            {
                this.activeForagerTargets[workerId] = currentCandidate with { ApproachTile = resolvedTarget.Tile };
                this.foragerSearches.Remove(workerId);
                this.activePhases[workerId] = WorkerTravelPhase.TravellingToFarmTarget;
                this.retryAfterTicks.Remove(workerId);
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Traveling",
                    $"Walking to {WorkerForageAreaCatalog.GetDisplayName(currentCandidate.LocationName)} {currentCandidate.ResourceTile}",
                    0, currentCandidate.ResourceTile);
                this.LogState(workerId, worker,
                    $"fallback planner recovered reachable {assignment} target {currentCandidate.LocationName} "
                    + $"{currentCandidate.ResourceTile} via {resolvedTarget.Tile}");
                return;
            }

            this.monitor.Log($"{worker.displayName} fallback planner found a clear route to eligible {assignment} target "
                + $"{currentCandidate.ResourceTile} via {reachableApproach}, but ordinary route planning still failed.", LogLevel.Info);
        }

        if (status == WorkerObstaclePlanStatus.BlockedByLargeObstacle)
            this.workerShellManager.ReportLargeObstacle(workerId, assignment, search.TargetLocation.NameOrUniqueName);

        this.monitor.Log($"{worker.displayName} could not reach {assignment} work in {search.TargetLocation.NameOrUniqueName}; route obstacle check={status}. Returning home.", LogLevel.Info);
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
    }

    private void LogFailedHardwoodCandidate(NPC worker, ForagerSearchState search, ForagerTarget target)
    {
        search.HardwoodFailureDiagnosticCount++;
        if (search.HardwoodFailureDiagnosticCount <= 8)
        {
            string route = this.navigationManager.GetLocalRouteDiagnostic(worker, target.ApproachTile);
            this.monitor.Log($"Hardwood candidate route failed ({search.HardwoodFailureDiagnosticCount}/8 details): "
                + $"resource={target.ResourceTile}; kind={target.Kind}; approach={target.ApproachTile}; {route}", LogLevel.Info);
        }
        else if (search.HardwoodFailureDiagnosticCount == 9)
        {
            this.monitor.Log("Hardwood candidate route details suppressed after 8 failures in this search sweep.", LogLevel.Info);
        }
    }

    private void UpdateObstacleClear(NPC worker, string workerId, WorkerTaskKind assignment, ActiveObstacleClear clear)
    {
        if (clear.IsAssignedDebrisJob
            && (assignment != WorkerTaskKind.ClearDebris
                || !this.workerShellManager.CanWorkerWorkToday(workerId)
                || !WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay)))
        {
            this.activeObstacleClears.Remove(workerId);
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        if (worker.controller is not null || this.navigationManager.HasActiveRoute(workerId))
            return;

        if (worker.currentLocation != clear.Location || worker.TilePoint != clear.ApproachTile
            || Math.Abs(worker.TilePoint.X - clear.ObstacleTile.X) + Math.Abs(worker.TilePoint.Y - clear.ObstacleTile.Y) != 1)
        {
            this.monitor.Log($"{worker.displayName} could not reach small route debris at {clear.ObstacleTile}; "
                + (clear.ContinueReturnHome ? "checking return route." : "replanning work."), LogLevel.Info);
            if (clear.ContinueReturnHome)
            {
                this.activeObstacleClears.Remove(workerId);
                this.TryEmergencyReturnHome(worker, workerId, clear.ReturnReason, "could not reach clearable exit-route debris");
                return;
            }
            this.RetryWorkAfterFailedDebris(worker, workerId, clear);
            return;
        }

        if (!clear.Location.objects.TryGetValue(clear.ObstacleTile.ToVector2(), out StardewValley.Object? item)
            || !ReferenceEquals(item, clear.ExpectedObject))
        {
            this.activeObstacleClears.Remove(workerId);
            if (clear.ContinueReturnHome)
            {
                this.BeginReturnHome(worker, workerId, clear.ReturnReason);
                return;
            }
            this.foragerSearches.Remove(workerId);
            return;
        }

        if (clear.IsAssignedDebrisJob
            ? !WorkerRouteObstacleClassifier.IsSmallLitter(item)
            : !this.navigationManager.IsSmallRouteObstacle(worker, clear.ObstacleTile))
        {
            if (clear.ContinueReturnHome)
            {
                this.activeObstacleClears.Remove(workerId);
                this.BeginReturnHome(worker, workerId, clear.ReturnReason);
                return;
            }
            this.RetryWorkAfterFailedDebris(worker, workerId, clear);
            return;
        }

        this.AnimateForagerSwing(worker, clear.ObstacleTile);
        if (item.shakeTimer > 0)
        {
            if (clear.CooldownWaitStartedAtTick < 0)
            {
                clear.CooldownWaitStartedAtTick = Game1.ticks;
                clear.CooldownWaitLimitTicks = item.shakeTimer + ForagerSwingIntervalTicks;
            }
            if (Game1.ticks - clear.CooldownWaitStartedAtTick > clear.CooldownWaitLimitTicks)
            {
                this.monitor.Log($"{worker.displayName} stopped clearing route debris at {clear.ObstacleTile}; the tool cooldown did not finish.", LogLevel.Warn);
                if (clear.ContinueReturnHome)
                {
                    this.activeObstacleClears.Remove(workerId);
                    this.TryEmergencyReturnHome(worker, workerId, clear.ReturnReason, "exit-route debris cooldown timed out");
                    return;
                }
                this.RetryWorkAfterFailedDebris(worker, workerId, clear);
            }
            return;
        }
        clear.CooldownWaitStartedAtTick = -1;
        if (Game1.ticks < clear.NextSwingTick)
            return;

        StardewValley.Tool tool = item.IsWeeds() ? new MeleeWeapon("47")
            : item.IsTwig() ? new Axe()
            : new Pickaxe();
        tool.lastUser = Game1.MasterPlayer;
        clear.ToolActionCount++;
        clear.NextSwingTick = Game1.ticks + ForagerSwingIntervalTicks;
        HashSet<Debris> beforeSwing = clear.Location.debris.ToHashSet();
        int durabilityBefore = item.MinutesUntilReady;
        bool toolActionCompleted;
        try
        {
            toolActionCompleted = WithoutFarmerExperience(() => item.performToolAction(tool));
        }
        catch (Exception ex)
        {
            this.monitor.Log($"{worker.displayName} could not clear route debris at {clear.ObstacleTile}: {ex.Message}", LogLevel.Warn);
            if (clear.ContinueReturnHome)
            {
                this.activeObstacleClears.Remove(workerId);
                this.TryEmergencyReturnHome(worker, workerId, clear.ReturnReason, "exit-route debris action failed");
                return;
            }
            this.RetryWorkAfterFailedDebris(worker, workerId, clear);
            return;
        }

        clear.CapturedDebris.UnionWith(WorkerDebrisCapture.CaptureNew(beforeSwing, clear.Location.debris));

        int durabilityAfter = item.MinutesUntilReady;
        bool remains = clear.Location.objects.TryGetValue(clear.ObstacleTile.ToVector2(), out StardewValley.Object? remaining)
            && ReferenceEquals(remaining, item);
        WorkerObstacleToolActionStatus actionStatus = WorkerObstacleToolProgressPolicy.Evaluate(
            toolActionCompleted, remains, durabilityBefore, durabilityAfter, clear.ToolActionCount, clear.MaxToolActions);
        if (actionStatus == WorkerObstacleToolActionStatus.Completed)
        {
            if (toolActionCompleted)
            {
                if (item.IsBreakableStone())
                    WithoutFarmerExperience(() => clear.Location.OnStoneDestroyed(item.ItemId,
                        clear.ObstacleTile.X, clear.ObstacleTile.Y, Game1.MasterPlayer));
                WithoutFarmerExperience(() => item.performRemoveAction());
                if (clear.Location.objects.TryGetValue(clear.ObstacleTile.ToVector2(), out StardewValley.Object? stillPresent)
                    && ReferenceEquals(stillPresent, item))
                    clear.Location.objects.Remove(clear.ObstacleTile.ToVector2());
            }
            clear.CapturedDebris.UnionWith(WorkerDebrisCapture.CaptureNew(beforeSwing, clear.Location.debris));
        }
        else if (actionStatus is WorkerObstacleToolActionStatus.NoProgress or WorkerObstacleToolActionStatus.ActionLimitReached)
        {
            this.monitor.Log($"{worker.displayName} could not clear route debris at {clear.ObstacleTile}; tool progress={durabilityBefore}->{durabilityAfter}, actions={clear.ToolActionCount}/{clear.MaxToolActions}.", LogLevel.Warn);
            if (clear.ContinueReturnHome)
            {
                this.activeObstacleClears.Remove(workerId);
                this.TryEmergencyReturnHome(worker, workerId, clear.ReturnReason, "exit-route debris made no further tool progress");
                return;
            }
            this.RetryWorkAfterFailedDebris(worker, workerId, clear);
            return;
        }
        else
        {
            if (item.shakeTimer > 0)
            {
                clear.CooldownWaitStartedAtTick = Game1.ticks;
                clear.CooldownWaitLimitTicks = item.shakeTimer + ForagerSwingIntervalTicks;
            }
            this.monitor.Log($"{worker.displayName} used a route-debris tool at {clear.ObstacleTile}; completed={toolActionCompleted}, durability={durabilityBefore}->{durabilityAfter}, cooldown={item.shakeTimer}, actions={clear.ToolActionCount}/{clear.MaxToolActions}.", LogLevel.Trace);
            return;
        }

        if (clear.CapturedDebris.Count > 0)
            this.dropCollectionZones.Add(new DropCollectionZone(clear.Location, clear.ObstacleTile,
                new HashSet<Debris>(), Game1.ticks + 1, Game1.ticks + 60,
                workerId, worker.displayName, assignment, ForagerTargetKind.RouteDebris, clear.CapturedDebris));

        this.monitor.Log($"{worker.displayName} cleared small route debris at {clear.Location.NameOrUniqueName} {clear.ObstacleTile}; captured {clear.CapturedDebris.Count} drop groups.", LogLevel.Info);
        if (clear.IsAssignedDebrisJob)
        {
            this.RecordCompletedWork(workerId);
            this.workerShellManager.TryAwardCompletedActionExperience(workerId,
                item.IsBreakableStone() ? WorkerExperienceAction.ClearStone
                : item.IsTwig() ? WorkerExperienceAction.ClearTwig : WorkerExperienceAction.ClearWeeds);
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Working",
                $"Cleared debris at {clear.ObstacleTile}", 0, clear.ObstacleTile);
        }
        this.activeObstacleClears.Remove(workerId);
        this.foragerSearches.Remove(workerId);
        this.navigationManager.ApplyIdlePose(worker);
        if (clear.ContinueReturnHome)
            this.BeginReturnHome(worker, workerId, clear.ReturnReason);
    }

    private void RetryWorkAfterFailedDebris(NPC worker, string workerId, ActiveObstacleClear clear)
    {
        this.navigationManager.StopTravel(worker);
        this.activeObstacleClears.Remove(workerId);
        this.foragerSearches.Remove(workerId);
        if (clear.CapturedDebris.Count > 0)
            this.dropCollectionZones.Add(new DropCollectionZone(clear.Location, clear.ObstacleTile,
                new HashSet<Debris>(), Game1.ticks + 1, Game1.ticks + 60,
                workerId, worker.displayName, this.workerShellManager.GetAssignedTask(workerId),
                ForagerTargetKind.RouteDebris, clear.CapturedDebris));

        HashSet<Point> failed = this.failedWorkDebris.GetValueOrDefault(workerId) ?? new HashSet<Point>();
        this.failedWorkDebris[workerId] = failed;
        failed.Add(clear.ObstacleTile);
        if (failed.Count >= MaxFailedWorkDebrisApproaches
            && this.workerShellManager.GetAssignedTask(workerId) != WorkerTaskKind.ClearDebris)
        {
            this.monitor.Log($"{worker.displayName} exhausted {failed.Count} failed work-route debris approaches; returning home.", LogLevel.Info);
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
            "Checking", "Rechecking work routes after blocked debris", 0, clear.ObstacleTile);
    }

    private IEnumerable<ForagerTarget> GetForagerTargets(NPC worker, string workerId, GameLocation location,
        WorkerTaskKind assignment, bool includeBlockedApproaches = false)
    {
        if (assignment == WorkerTaskKind.ClearDebris)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                if (!WorkerRouteObstacleClassifier.IsSmallLitter(pair.Value))
                    continue;

                Point debrisTile = pair.Key.ToPoint();
                if (this.failedWorkDebris.GetValueOrDefault(workerId)?.Contains(debrisTile) == true
                    || this.IsDebrisReservedByAnother(workerId, location, debrisTile))
                    continue;

                foreach (Point approach in this.GetApproachTiles(worker, location, debrisTile, 1, 1, includeBlockedApproaches))
                {
                    if (Math.Abs(approach.X - debrisTile.X) + Math.Abs(approach.Y - debrisTile.Y) == 1)
                        yield return new ForagerTarget(location.NameOrUniqueName, debrisTile, approach,
                            ForagerTargetKind.RouteDebris, pair.Value);
                }
            }
            yield break;
        }

        if (assignment == WorkerTaskKind.CollectForage)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                if (!pair.Value.IsSpawnedObject || !pair.Value.isForage())
                    continue;

                Point resourceTile = pair.Key.ToPoint();
                foreach (Point approach in this.GetApproachTiles(worker, location, resourceTile, 1, 1, includeBlockedApproaches))
                    yield return new ForagerTarget(location.NameOrUniqueName, resourceTile, approach, ForagerTargetKind.Forage);
            }
            yield break;
        }

        foreach (KeyValuePair<Vector2, TerrainFeature> pair in location.terrainFeatures.Pairs)
        {
            if (pair.Value is not Tree tree || tree.growthStage.Value < 5 || tree.falling.Value || tree.health.Value <= -99f || tree.tapped.Value)
                continue;

            bool hardwood = IsHardwoodTree(tree);
            if (assignment == WorkerTaskKind.ChopTrees && hardwood)
                continue;
            if (assignment == WorkerTaskKind.ChopHardwood && !hardwood)
                continue;

            Point resourceTile = pair.Key.ToPoint();
            foreach (Point approach in this.GetApproachTiles(worker, location, resourceTile, 1, 1, includeBlockedApproaches))
                yield return new ForagerTarget(location.NameOrUniqueName, resourceTile, approach,
                    hardwood ? ForagerTargetKind.HardwoodTree : ForagerTargetKind.Tree);
        }

        if (assignment != WorkerTaskKind.ChopHardwood)
            yield break;

        foreach (ResourceClump clump in location.resourceClumps)
        {
            if (clump.parentSheetIndex.Value is not ResourceClump.stumpIndex and not ResourceClump.hollowLogIndex)
                continue;

            Point resourceTile = clump.Tile.ToPoint();
            foreach (Point approach in this.GetApproachTiles(worker, location, resourceTile, clump.width.Value, clump.height.Value, includeBlockedApproaches))
                yield return new ForagerTarget(location.NameOrUniqueName, resourceTile, approach, ForagerTargetKind.HardwoodClump);
        }
    }

    private bool IsDebrisReservedByAnother(string workerId, GameLocation location, Point tile)
    {
        foreach (KeyValuePair<string, ForagerTarget> pair in this.activeForagerTargets)
        {
            if (!string.Equals(pair.Key, workerId, StringComparison.OrdinalIgnoreCase)
                && pair.Value.Kind == ForagerTargetKind.RouteDebris
                && pair.Value.LocationName == location.NameOrUniqueName
                && pair.Value.ResourceTile == tile)
                return true;
        }

        foreach (KeyValuePair<string, ActiveObstacleClear> pair in this.activeObstacleClears)
        {
            if (!string.Equals(pair.Key, workerId, StringComparison.OrdinalIgnoreCase)
                && pair.Value.Location == location && pair.Value.ObstacleTile == tile)
                return true;
        }

        return false;
    }

    private IEnumerable<Point> GetApproachTiles(NPC worker, GameLocation location, Point resourceTile,
        int width, int height, bool includeBlockedApproaches)
        => WorkerApproachTiles.AroundResource(resourceTile, width, height, tile => includeBlockedApproaches
            ? location.isTileOnMap(tile.ToVector2())
            : this.navigationManager.IsWalkableWorkTile(location, worker, tile));

    private void BeginForagerAction(
        NPC worker,
        string workerId,
        GameLocation location,
        ForagerTarget target,
        WorkerTaskKind assignment)
    {
        if (assignment == WorkerTaskKind.ClearDebris && target.Kind == ForagerTargetKind.RouteDebris)
        {
            if (!location.objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? item)
                || !ReferenceEquals(item, target.ExpectedObject)
                || !WorkerRouteObstacleClassifier.IsSmallLitter(item)
                || this.IsDebrisReservedByAnother(workerId, location, target.ResourceTile))
            {
                this.FinishForagerAction(worker, workerId, target, assignment, null);
                return;
            }

            this.navigationManager.StopWorker(worker);
            this.activeForagerTargets.Remove(workerId);
            this.activeObstacleClears[workerId] = new ActiveObstacleClear
            {
                Location = location,
                ObstacleTile = target.ResourceTile,
                ApproachTile = target.ApproachTile,
                ExpectedObject = item,
                NextSwingTick = Game1.ticks,
                MaxToolActions = WorkerObstacleToolProgressPolicy.GetMaximumActions(
                    item.MinutesUntilReady, damagePerToolAction: 1, hardLimit: MaxDebrisToolActions),
                IsAssignedDebrisJob = true,
            };
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Clearing",
                $"Clearing debris in {WorkerForageAreaCatalog.GetDisplayName(location.NameOrUniqueName)}", 0, target.ResourceTile);
            this.monitor.Log($"{worker.displayName} started clearing assigned debris at {location.NameOrUniqueName} {target.ResourceTile} from {target.ApproachTile}.", LogLevel.Info);
            return;
        }

        if (target.Kind == ForagerTargetKind.Forage)
        {
            if (location.Objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? forage)
                && forage.IsSpawnedObject && forage.isForage())
            {
                location.Objects.Remove(target.ResourceTile.ToVector2());
                bool chestOnly = WorkerItemStorage.Store(forage, this.workerShellManager.GetHarvestDestination(), this.monitor);
                this.RecordCompletedWork(workerId);
                this.workerShellManager.TryAwardCompletedActionExperience(workerId, WorkerExperienceAction.PickupForage);
                this.monitor.Log(
                    $"Worker {worker.displayName} [{workerId}] {assignment} forage at {target.LocationName} {target.ResourceTile}: collected; "
                    + $"storage={(chestOnly ? "selected chest" : "shipping bin, wholly or partly")}.",
                    LogLevel.Info);
                location.playSound("pickUpItem");
            }

            this.FinishForagerAction(worker, workerId, target, assignment, null);
            return;
        }

        this.navigationManager.StopWorker(worker);
        this.FaceWorkerToward(worker, target.ResourceTile);
        this.activeForagerActions[workerId] = new ActiveForagerAction
        {
            Target = target,
            Assignment = assignment,
            ExistingDebris = location.debris.ToHashSet(),
            NextSwingTick = Game1.ticks,
        };
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(
            assignment,
            "Working",
            $"Chopping at {target.ResourceTile}",
            0,
            target.ResourceTile);
        this.monitor.Log(
            $"{worker.displayName} started timed {assignment} action at {target.LocationName} {target.ResourceTile}; kind={target.Kind}.",
            LogLevel.Trace);
    }

    private void UpdateForagerAction(NPC worker, string workerId, ActiveForagerAction action)
    {
        GameLocation? location = Game1.getLocationFromName(action.Target.LocationName);
        if (location is null
            || worker.currentLocation != location
            || worker.TilePoint != action.Target.ApproachTile
            || this.workerShellManager.GetAssignedTask(workerId) != action.Assignment)
        {
            this.monitor.Log(
                $"Worker {worker.displayName} [{workerId}] {action.Assignment} {action.Target.Kind} at "
                + $"{action.Target.LocationName} {action.Target.ResourceTile}: action stopped before completion.",
                LogLevel.Info);
            this.FinishForagerAction(worker, workerId, action.Target, action.Assignment, null);
            return;
        }

        this.AnimateForagerSwing(worker, action.Target.ResourceTile);
        if (Game1.ticks < action.NextSwingTick)
            return;

        action.NextSwingTick = Game1.ticks + ForagerSwingIntervalTicks;
        action.SwingCount++;
        bool resourcePresent = action.Target.Kind == ForagerTargetKind.HardwoodClump
            ? location.resourceClumps.Any(clump => clump.Tile.ToPoint() == action.Target.ResourceTile
                && clump.parentSheetIndex.Value is ResourceClump.stumpIndex or ResourceClump.hollowLogIndex)
            : location.terrainFeatures.TryGetValue(action.Target.ResourceTile.ToVector2(), out TerrainFeature? feature)
                && feature is Tree tree && !tree.falling.Value
                && (IsHardwoodTree(tree) == (action.Assignment == WorkerTaskKind.ChopHardwood));
        Axe axe = new() { UpgradeLevel = 4, lastUser = Game1.MasterPlayer };
        bool finished = WithoutFarmerExperience(() => action.Target.Kind == ForagerTargetKind.HardwoodClump
            ? this.SwingAtHardwoodClump(location, action.Target, axe)
            : this.SwingAtTree(location, action.Target, action.Assignment, axe));

        this.monitor.Log(
            $"{worker.displayName} performed {action.Assignment} swing {action.SwingCount} at {action.Target.LocationName} {action.Target.ResourceTile}; finished={finished}.",
            LogLevel.Trace);
        if (finished)
        {
            if (resourcePresent)
            {
                this.RecordCompletedWork(workerId);
                this.workerShellManager.TryAwardCompletedActionExperience(workerId,
                    action.Target.Kind == ForagerTargetKind.HardwoodClump || action.Assignment == WorkerTaskKind.ChopHardwood
                        ? WorkerExperienceAction.ChopHardwood : WorkerExperienceAction.ChopTree);
            }
            string result = !resourcePresent ? "target unavailable"
                : action.Target.Kind == ForagerTargetKind.HardwoodClump ? "clump removed"
                : "tree chop completed";
            this.monitor.Log(
                $"Worker {worker.displayName} [{workerId}] {action.Assignment} {action.Target.Kind} at "
                + $"{action.Target.LocationName} {action.Target.ResourceTile}: {result}; drop scan pending.",
                LogLevel.Info);
            this.FinishForagerAction(worker, workerId, action.Target, action.Assignment, action);
        }
    }

    private bool SwingAtHardwoodClump(GameLocation location, ForagerTarget target, Axe axe)
    {
        ResourceClump? clump = location.resourceClumps.FirstOrDefault(candidate => candidate.Tile.ToPoint() == target.ResourceTile
            && candidate.parentSheetIndex.Value is ResourceClump.stumpIndex or ResourceClump.hollowLogIndex);
        if (clump is null)
            return true;

        if (clump.performToolAction(axe, 1, target.ResourceTile.ToVector2()))
        {
            clump.destroy(axe, location, target.ResourceTile.ToVector2());
            location.resourceClumps.Remove(clump);
            return true;
        }

        return false;
    }

    private bool SwingAtTree(GameLocation location, ForagerTarget target, WorkerTaskKind assignment, Axe axe)
    {
        if (!location.terrainFeatures.TryGetValue(target.ResourceTile.ToVector2(), out TerrainFeature? feature) || feature is not Tree tree)
            return true;

        bool isHardwood = IsHardwoodTree(tree);
        if ((assignment == WorkerTaskKind.ChopTrees && isHardwood)
            || (assignment == WorkerTaskKind.ChopHardwood && !isHardwood)
            || tree.falling.Value)
        {
            return true;
        }

        axe.swingTicker++;
        if (tree.performToolAction(axe, 0, target.ResourceTile.ToVector2()))
        {
            location.terrainFeatures.Remove(target.ResourceTile.ToVector2());
            return true;
        }

        if (!tree.falling.Value)
            return false;

        // A location without the local player may not advance a falling-tree animation.
        // Settle it immediately so the worker doesn't wait forever for an off-screen tree.
        if (location != Game1.currentLocation)
        {
            GameTime simulatedFrame = new(System.TimeSpan.Zero, System.TimeSpan.FromMilliseconds(16));
            for (int frame = 0; frame < 360 && tree.falling.Value; frame++)
                tree.tickUpdate(simulatedFrame);
        }

        return true;
    }

    private void FinishForagerAction(
        NPC worker,
        string workerId,
        ForagerTarget target,
        WorkerTaskKind assignment,
        ActiveForagerAction? action)
    {
        if (action is not null && Game1.getLocationFromName(target.LocationName) is GameLocation location)
        {
            this.dropCollectionZones.Add(new DropCollectionZone(
                location,
                target.ResourceTile,
                action.ExistingDebris,
                Game1.ticks + 10,
                Game1.ticks + 480,
                workerId,
                worker.displayName,
                assignment,
                target.Kind));
        }

        this.activeForagerActions.Remove(workerId);
        this.activeForagerTargets.Remove(workerId);
        this.retryAfterTicks.Remove(workerId);
        this.navigationManager.ApplyIdlePose(worker);
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(
            assignment,
            "Working",
            $"Finished {WorkerTaskPolicy.GetTaskLabel(assignment).ToLowerInvariant()}",
            0,
            target.ResourceTile);
    }

    private void AnimateForagerSwing(NPC worker, Point resourceTile)
    {
        this.FaceWorkerToward(worker, resourceTile);
        if (worker.Sprite is null)
            return;

        int baseFrame = worker.FacingDirection switch
        {
            1 => 4,
            0 => 8,
            3 => 12,
            _ => 0,
        };
        int[] swingFrames = { 0, 1, 0, 3 };
        worker.Sprite.StopAnimation();
        worker.Sprite.CurrentFrame = baseFrame + swingFrames[(Game1.ticks / 6) % swingFrames.Length];
        worker.Sprite.UpdateSourceRect();
    }

    private void FaceWorkerToward(NPC worker, Point resourceTile)
    {
        int deltaX = resourceTile.X - worker.TilePoint.X;
        int deltaY = resourceTile.Y - worker.TilePoint.Y;
        worker.FacingDirection = Math.Abs(deltaX) > Math.Abs(deltaY)
            ? deltaX >= 0 ? 1 : 3
            : deltaY >= 0 ? 2 : 0;
    }

    private static bool IsHardwoodTree(Tree tree)
    {
        // DropHardwoodOnLumberChop is a Lumberjack-profession bonus used by ordinary
        // trees too. Mahogany is the actual hardwood tree (legacy ID 8 / seed 292).
        return tree.treeType.Value == "8"
            || string.Equals(tree.GetData()?.SeedItemId, "(O)292", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool HasFallingTree(GameLocation location, WorkerTaskKind assignment)
    {
        foreach (TerrainFeature feature in location.terrainFeatures.Values)
        {
            if (feature is not Tree tree || !tree.falling.Value)
                continue;
            bool hardwood = IsHardwoodTree(tree);
            if ((assignment == WorkerTaskKind.ChopHardwood && hardwood) || (assignment == WorkerTaskKind.ChopTrees && !hardwood))
                return true;
        }
        return false;
    }

    private void UpdateDropCollectionZones()
    {
        if (this.dropCollectionZones.Count == 0)
            return;

        System.Diagnostics.Stopwatch collectionTime = System.Diagnostics.Stopwatch.StartNew();
        int activeZones = this.dropCollectionZones.Count;
        int storedStacks = 0;
        for (int i = this.dropCollectionZones.Count - 1; i >= 0; i--)
        {
            DropCollectionZone zone = this.dropCollectionZones[i];
            if (Game1.ticks >= zone.CollectAfterTick)
            {
                for (int debrisIndex = zone.Location.debris.Count - 1; debrisIndex >= 0; debrisIndex--)
                {
                    Debris debris = zone.Location.debris[debrisIndex];
                    bool belongsToZone = WorkerDebrisCapture.BelongsToZone(debris, zone.ExistingDebris,
                        zone.CapturedDebris, zone.CapturedDebris is null && this.IsDebrisNear(debris, zone.Tile, 9));
                    if (!belongsToZone || !this.TryCreateDebrisItem(debris, out Item? item))
                        continue;

                    if (!zone.StorageStartedLogged)
                    {
                        this.monitor.Log(
                            $"Worker {zone.WorkerName} [{zone.WorkerId}] {zone.Assignment} {zone.Kind} at "
                            + $"{zone.Location.NameOrUniqueName} {zone.Tile}: storing first nearby drop.",
                            LogLevel.Info);
                        zone.StorageStartedLogged = true;
                    }
                    System.Diagnostics.Stopwatch storageTime = System.Diagnostics.Stopwatch.StartNew();
                    bool chestOnly;
                    try
                    {
                        chestOnly = WorkerItemStorage.Store(item!, this.workerShellManager.GetHarvestDestination(), this.monitor);
                    }
                    catch (Exception ex)
                    {
                        this.monitor.Log($"Worker could not store debris at {zone.Location.NameOrUniqueName} {zone.Tile}; leaving it on the ground: {ex.Message}", LogLevel.Warn);
                        continue;
                    }
                    zone.Location.debris.RemoveAt(debrisIndex);
                    storageTime.Stop();
                    if (storageTime.ElapsedMilliseconds >= 100)
                        this.monitor.Log($"Worker drop storage took {storageTime.ElapsedMilliseconds} ms at {zone.Location.NameOrUniqueName} {zone.Tile}.", LogLevel.Info);
                    if (chestOnly)
                        zone.ChestOnlyStacks++;
                    else
                        zone.ShippedStacks++;
                    storedStacks++;
                }
            }

            if (Game1.ticks >= zone.ExpiresAtTick)
            {
                this.monitor.Log(
                    $"Worker {zone.WorkerName} [{zone.WorkerId}] {zone.Assignment} {zone.Kind} drops at "
                    + $"{zone.Location.NameOrUniqueName} {zone.Tile}: {zone.ChestOnlyStacks} stacks to selected chest, "
                    + $"{zone.ShippedStacks} stacks shipped wholly or partly.",
                    LogLevel.Info);
                this.dropCollectionZones.RemoveAt(i);
            }
        }
        if (collectionTime.ElapsedMilliseconds >= 100)
            this.monitor.Log($"Worker drop collection pass took {collectionTime.ElapsedMilliseconds} ms across {activeZones} zones; stored {storedStacks} stacks.", LogLevel.Info);
    }

    private bool IsDebrisNear(Debris debris, Point tile, int radius)
    {
        foreach (Chunk chunk in debris.Chunks)
        {
            Point chunkTile = (chunk.position.Value / Game1.tileSize).ToPoint();
            if (Math.Abs(chunkTile.X - tile.X) <= radius && Math.Abs(chunkTile.Y - tile.Y) <= radius)
                return true;
        }
        return false;
    }

    private bool TryCreateDebrisItem(Debris debris, out Item? item)
    {
        item = null;
        if (debris.debrisType.Value is not Debris.DebrisType.OBJECT and not Debris.DebrisType.RESOURCE and not Debris.DebrisType.ARCHAEOLOGY)
            return false;

        if (debris.item is Item exactItem)
        {
            item = exactItem;
            return true;
        }
        if (string.IsNullOrWhiteSpace(debris.itemId.Value))
            return false;

        item = ItemRegistry.Create(debris.itemId.Value, Math.Max(1, debris.Chunks.Count), debris.itemQuality);
        return true;
    }

    private bool TryFindCropTarget(NPC worker, string workerId, Farm farm, WorkerTaskKind assignment, WorkerTaskKind desiredAction, out Point target)
    {
        target = Point.Zero;
        int shortestRoute = int.MaxValue;
        bool foundTarget = false;
        Point fallbackTarget = Point.Zero;
        int shortestFallbackDistance = int.MaxValue;
        foreach (KeyValuePair<Vector2, TerrainFeature> pair in farm.terrainFeatures.Pairs.OrderBy(p => p.Key.X + p.Key.Y))
        {
            Point candidate = pair.Key.ToPoint();
            if (this.failedCropTargets.TryGetValue(workerId, out Dictionary<Point, int>? failed)
                && failed.TryGetValue(candidate, out int retryAt) && Game1.ticks < retryAt)
                continue;
            if (!this.IsEligibleCropTarget(workerId, farm, candidate, assignment, desiredAction))
                continue;

            int fallbackDistance = Math.Abs(worker.TilePoint.X - candidate.X) + Math.Abs(worker.TilePoint.Y - candidate.Y);
            if (fallbackDistance < shortestFallbackDistance)
            {
                shortestFallbackDistance = fallbackDistance;
                fallbackTarget = candidate;
            }

            if (worker.currentLocation != farm
                || !this.navigationManager.TryGetLocalRouteLength(worker, candidate, out int routeLength))
                continue;

            if (routeLength >= shortestRoute)
                continue;

            shortestRoute = routeLength;
            target = candidate;
            foundTarget = true;
        }

        if (foundTarget)
            return true;

        if (shortestFallbackDistance < int.MaxValue)
        {
            target = fallbackTarget;
            if (!this.lastCropFallbackTargets.TryGetValue(workerId, out Point previous) || previous != target)
            {
                this.lastCropFallbackTargets[workerId] = target;
                this.monitor.Log($"No local route length available for {desiredAction} from {worker.currentLocation?.NameOrUniqueName}; "
                    + $"trying crop {target} through an existing warp.", LogLevel.Trace);
            }
            return true;
        }

        return false;
    }

    private bool IsEligibleCropTarget(string workerId, Farm farm, Point tile, WorkerTaskKind assignment, WorkerTaskKind desiredAction)
    {
        if (this.activeTargets.Any(pair => !string.Equals(pair.Key, workerId, System.StringComparison.OrdinalIgnoreCase)
                && pair.Value == tile)
            || desiredAction == WorkerTaskKind.HarvestCrops && this.blockedHarvestTiles.Contains(tile)
            || !farm.terrainFeatures.TryGetValue(tile.ToVector2(), out TerrainFeature? feature)
            || feature is not HoeDirt dirt || dirt.crop is null || dirt.crop.dead.Value)
            return false;

        return WorkerTaskPolicy.SelectCropAction(assignment, true, IsHarvestable(dirt.crop), !dirt.isWatered()) == desiredAction;
    }

    private void StartJobTravel(NPC worker, string workerId, Point target, WorkerTaskKind task, string reason)
    {
        if (!this.CanRetryTravel(workerId))
        {
            return;
        }

        this.activeTargets[workerId] = target;
        this.activePhases[workerId] = WorkerTravelPhase.TravellingToFarmTarget;
        this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(task, "Traveling", $"Walking to crop at {target}", 0, target);
        this.LogState(workerId, worker, $"selected {task} target tile {target}; starting route ({reason})");
        WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
        Farm farm = Game1.getFarm();
        if (worker.currentLocation == farm && WorkerCropApproachPolicy.IsInWorkRange(worker.TilePoint, target))
        {
            this.retryAfterTicks.Remove(workerId);
            return;
        }
        Point travelTile = target;
        if (worker.currentLocation == farm)
        {
            int shortest = int.MaxValue;
            foreach (Point approach in WorkerCropApproachPolicy.GetApproaches(target))
            {
                if (!this.navigationManager.TryGetLocalRouteLength(worker, approach, out int length) || length >= shortest)
                    continue;
                travelTile = approach;
                shortest = length;
            }
            if (shortest == int.MaxValue)
            {
                this.MarkFailedCropTarget(workerId, target);
                this.activeTargets.Remove(workerId);
                this.monitor.Log($"{worker.displayName} found no walkable approach to crop {target}; trying another crop.", LogLevel.Info);
                return;
            }
        }

        if (!this.navigationManager.TryStartTravel(worker, new WorkerNavigationTarget("Farm", travelTile, 2), reason,
                out WorkerNavigationTarget resolvedTarget,
                landingValidator: point => WorkerCropApproachPolicy.IsNearbyLanding(point, target,
                    this.IsEligibleCropTarget(workerId, farm, target, assignment, task))))
        {
            this.LogState(workerId, worker, $"route start failed for target tile {target}; clearing target and waiting for next update");
            this.MarkFailedCropTarget(workerId, target);
            this.activeTargets.Remove(workerId);
            this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
            return;
        }

        this.retryAfterTicks.Remove(workerId);
    }

    private void MarkFailedCropTarget(string workerId, Point target)
    {
        Dictionary<Point, int> failed = this.failedCropTargets.GetValueOrDefault(workerId) ?? new Dictionary<Point, int>();
        this.failedCropTargets[workerId] = failed;
        failed[target] = Game1.ticks + FailedCropRetryTicks;
    }

    private bool PerformJobAt(string workerId, Farm farm, Point tile, WorkerTaskKind task)
    {
        if (!farm.terrainFeatures.TryGetValue(tile.ToVector2(), out TerrainFeature? feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            this.monitor.Log($"Worker action skipped at tile {tile}: no HoeDirt/crop exists when the worker arrived.", LogLevel.Trace);
            return false;
        }
        this.monitor.Log($"Worker action check at tile {tile}: assignment={task}, watered={dirt.isWatered()}, harvestable={IsHarvestable(dirt.crop)}, crop={dirt.crop.indexOfHarvest.Value ?? "none"}.", LogLevel.Trace);
        if (task == WorkerTaskKind.WaterCrops && !dirt.isWatered())
        {
            dirt.state.Value = HoeDirt.watered;
            this.monitor.Log($"Worker watered crop tile {tile}.", LogLevel.Trace);
            return true;
        }
        if (task == WorkerTaskKind.HarvestCrops && IsHarvestable(dirt.crop))
        {
            Crop crop = dirt.crop;
            int regrowDaysBefore = crop.dayOfCurrentPhase.Value;
            this.monitor.Log($"Worker reached crop tile {tile}; harvesting crop {crop.indexOfHarvest.Value} (regrows={crop.RegrowsAfterHarvest()}).", LogLevel.Trace);
            WorkerHarvestCollector collector = new(farm, tile, this.workerShellManager.GetHarvestDestination(), this.monitor);
            int shippingBinCountBefore = farm.getShippingBin(Game1.MasterPlayer).Count;
            bool harvested = WithoutFarmerExperience(() => crop.harvest(tile.X, tile.Y, dirt, collector));
            int shippingBinCountAfter = farm.getShippingBin(Game1.MasterPlayer).Count;
            bool cropUnchanged = ReferenceEquals(dirt.crop, crop);
            bool confirmedHarvest = WorkerExperiencePolicy.IsConfirmedCropHarvest(harvested,
                crop.RegrowsAfterHarvest(), regrowDaysBefore, crop.dayOfCurrentPhase.Value,
                collector.ItemsCollected);

            if (harvested)
            {
                dirt.crop = null;
                this.monitor.Log($"Worker removed harvested crop at tile {tile}; crop was unchanged before removal={cropUnchanged}.", LogLevel.Trace);
            }
            else if (collector.ItemsCollected > 0 && cropUnchanged && !crop.RegrowsAfterHarvest())
            {
                this.blockedHarvestTiles.Add(tile);
                this.monitor.Log(
                    $"Worker harvest at tile {tile} produced an item but left the non-regrowing crop unchanged; "
                    + "blocking further harvest retries for this tile until reset.",
                    LogLevel.Warn);
            }

            this.monitor.Log(
                $"Worker harvest at tile {tile} completed={harvested}; crop remains={dirt.crop is not null}; "
                + $"items collected={collector.ItemsCollected}, chest deposits={collector.ChestDeposits}, "
                + $"shipping bin count={shippingBinCountBefore}->{shippingBinCountAfter}.",
                LogLevel.Trace);
            this.workerShellManager.TryAwardFarmingHarvestExperience(workerId, confirmedHarvest, collector.ItemsCollected);
            return harvested || collector.ItemsCollected > 0;
        }
        return false;
    }

    private void LogState(string workerId, NPC worker, string state)
    {
        if (this.lastDebugStates.TryGetValue(workerId, out string? previous) && string.Equals(previous, state, StringComparison.Ordinal))
            return;
        this.lastDebugStates[workerId] = state;
        this.monitor.Log($"{worker.displayName} [{workerId}] runtime: {state}; assignment={this.workerShellManager.GetAssignedTask(workerId)}; location={worker.currentLocation?.NameOrUniqueName ?? "unknown"}; tile={worker.TilePoint}; controller={worker.controller?.GetType().Name ?? "none"}.", LogLevel.Trace);
    }

    private void LogHarvestReadinessSamples(Farm farm)
    {
        int sampleCount = 0;
        foreach (KeyValuePair<Vector2, TerrainFeature> pair in farm.terrainFeatures.Pairs.OrderBy(p => p.Key.X + p.Key.Y))
        {
            if (sampleCount >= 3)
                break;
            if (pair.Value is not HoeDirt dirt || dirt.crop is null)
                continue;

            Point tile = pair.Key.ToPoint();
            Crop crop = dirt.crop;
            bool watered = dirt.isWatered();
            string state = $"seed={crop.netSeedIndex.Value};phase={crop.currentPhase.Value}/{crop.phaseDays.Count};show={crop.phaseToShow};"
                + $"fullyGrown={crop.fullyGrown.Value};day={crop.dayOfCurrentPhase.Value};regrows={crop.RegrowsAfterHarvest()};"
                + $"dead={crop.dead.Value};watered={watered};harvest={crop.indexOfHarvest.Value?.ToString() ?? "none"};"
                + $"isHarvestable={IsHarvestable(crop)}";
            if (this.lastHarvestReadinessStates.TryGetValue(tile, out string? previousState)
                && string.Equals(previousState, state, System.StringComparison.Ordinal))
            {
                sampleCount++;
                continue;
            }

            this.lastHarvestReadinessStates[tile] = state;
            this.monitor.Log(
                $"Harvest readiness sample at tile {tile}: {state}.",
                LogLevel.Trace);
            sampleCount++;
        }
    }

    private static bool IsHarvestable(Crop crop)
    {
        if (crop.indexOfHarvest is null)
            return false;

        if (crop.RegrowsAfterHarvest())
        {
            // A regrowing crop stays fully grown after harvest. Its day-of-current-phase
            // must be zero before it is available again.
            return crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value <= 0;
        }

        // Non-regrowing crops can be harvestable at their final phase before the
        // fullyGrown flag is set by the vanilla crop update.
        return crop.phaseDays.Count > 0 && crop.currentPhase.Value >= crop.phaseDays.Count - 1;
    }

    private sealed class WorkerHarvestCollector : JunimoHarvester
    {
        private readonly Farm farm;
        private readonly Chest? destinationChest;
        private readonly bool destinationSelected;
        private readonly IMonitor monitor;
        private bool fallbackLogged;

        public int ItemsCollected { get; private set; }

        public int ChestDeposits { get; private set; }

        public WorkerHarvestCollector(Farm farm, Point tile, WorkerHarvestDestination? destination, IMonitor monitor)
        {
            this.farm = farm;
            this.monitor = monitor;
            this.destinationSelected = destination is not null;
            if (destination is not null && WorkerChestCatalog.TryGetChest(destination, out Chest? chest))
                this.destinationChest = chest;
            this.currentLocation = farm;
            this.Position = tile.ToVector2() * Game1.tileSize;
        }

        public override void tryToAddItemToHut(Item item)
        {
            this.ItemsCollected++;
            WorkerItemStorage.StoreResult result = WorkerItemStorage.Store(item, this.destinationChest, this.farm, ex =>
                this.monitor.Log($"Worker could not add harvested items to the selected chest: {ex.Message}", LogLevel.Warn));
            if (result.ChestDeposit)
                this.ChestDeposits++;
            if (result.ShippedRemainder && this.destinationSelected && !this.fallbackLogged)
            {
                this.monitor.Log("Worker's selected chest was unavailable, locked, or full; remaining produce went to the shipping bin.", LogLevel.Warn);
                this.fallbackLogged = true;
            }
            this.farm.playSound("harvest");
        }
    }
}
