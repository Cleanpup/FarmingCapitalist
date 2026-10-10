using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Locations;
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
        LooseGem,
    }

    private sealed record ForagerTarget(string LocationName, Point ResourceTile, Point ApproachTile, ForagerTargetKind Kind,
        StardewValley.Object? ExpectedObject = null);

    private sealed record DropCollectionZone(
        GameLocation Location, Point Tile, HashSet<Debris> ExistingDebris, int CollectAfterTick, int ExpiresAtTick,
        string WorkerId, string WorkerName, WorkerTaskKind Assignment, ForagerTargetKind Kind,
        WorkerHarvestDestination? Destination,
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
    private readonly WorkerIdleMovementManager idleMovement;
    private readonly WorkerShellManager workerShellManager;
    private readonly WorkerCombatManager combatManager;
    private readonly WorkerDungeonTravelManager dungeonTravel;
    private readonly Dictionary<string, GameLocation> miningLocations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> miningTransitionRetry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WorkerMiningWorkMode> miningModes = new(StringComparer.OrdinalIgnoreCase);
    private readonly WorkerExplorationManager explorationManager;
    private readonly WorkerFishingManager fishingManager;
    private readonly WorkerWorkAnimationManager workAnimations;
    private readonly HashSet<string> staminaRestingWorkers = new(StringComparer.OrdinalIgnoreCase);
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
        this.idleMovement = new WorkerIdleMovementManager(navigationManager, workerShellManager, monitor);
        this.dungeonTravel = new(navigationManager, monitor);
        this.combatManager = new WorkerCombatManager(workerShellManager, navigationManager, monitor,
            this.RecordCompletedWork, this.dungeonTravel);
        this.explorationManager = new(workerShellManager, navigationManager, monitor);
        this.fishingManager = new(workerShellManager, navigationManager, monitor);
        this.workAnimations = new(workerShellManager, monitor);
        this.monitor = monitor;
    }

    public void HandleWorkerInitialized(NPC? worker, string triggerReason)
    {
        // The saved farmhouse tile is the overnight home. Idle workers head outdoors
        // during the day, while DayStarted places the morning crew on the Farm.
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

    public void PlaceWorkersOnFarmForMorning() => this.idleMovement.PlaceWorkersOnFarmForMorning();

    private bool CanWanderToday(string workerId)
        => this.workerShellManager.CanWorkerWorkToday(workerId) && !this.workerShellManager.WasDefeatedToday(workerId);

    private bool MayFollowDungeon(string workerId) =>
        !this.workerShellManager.GetWorkerStamina(workerId).Resting
        && this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None)
            is not (WorkerTravelPhase.ReturningToFarmhouse or WorkerTravelPhase.RestingAtFarmhouse
                or WorkerTravelPhase.CheckingHomeRoute or WorkerTravelPhase.BlockedAtLocation);

    public void HandleHostWarp()
    {
        this.combatManager.HandleHostWarp(this.MayFollowDungeon);
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers(recoverStale: false))
        {
            if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId)
                || !WorkerMiningPolicy.IsMiningTask(this.workerShellManager.GetAssignedTask(workerId))
                || !this.workerShellManager.CanWorkerWorkToday(workerId)
                || !WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay) || !this.MayFollowDungeon(workerId)) continue;
            string area = this.workerShellManager.GetMiningArea(workerId);
            if (!WorkerMiningPolicy.IsDungeon(area) || WorkerMiningAreaCatalog.AccessReason(area) is not null) continue;
            this.navigationManager.StopWorker(worker);
            this.ClearMiningFloorState(workerId);
            this.dungeonTravel.FollowHostWarp(worker, area, () => this.ClearMiningFloorState(workerId));
        }
    }

    public void Update()
    {
        this.navigationManager.Update();
        this.combatManager.UpdateClientAnimations();
        this.workAnimations.Update();
        this.fishingManager.UpdateVisuals();

        if (!Context.IsWorldReady)
        {
            return;
        }

        if (!Context.IsMainPlayer)
        {
            return;
        }

        if (!Game1.shouldTimePass() || Game1.activeClickableMenu is not null || Game1.CurrentEvent is not null)
        {
            // Observe paused clock changes, preventing a menu/event gap from paying out later.
            foreach (NPC pausedWorker in this.workerShellManager.GetSpawnedWorkers(recoverStale: false))
                if (this.workerShellManager.TryGetWorkerId(pausedWorker, out string pausedId))
                {
                    this.workerShellManager.ObserveWorkerStamina(pausedId, recovering: false);
                    if (this.workerShellManager.GetAssignedTask(pausedId) == WorkerTaskKind.Fish)
                        this.fishingManager.Suspend(pausedId);
                }
            return;
        }

        this.UpdateDropCollectionZones();

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
        {
            if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
            {
                continue;
            }

            this.combatManager.EnsureHealth(worker, workerId);
            if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.CombatWorker)
            {
                if (Game1.activeClickableMenu is null && Game1.CurrentEvent is null)
                    this.explorationManager.DeliverPendingLoot(workerId);
            }

            if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Fisher)
                this.fishingManager.Deliver(workerId);

            if (this.workerShellManager.GetWorkerProfession(workerId) is WorkerProfession.CombatWorker or WorkerProfession.Miner or WorkerProfession.Fisher
                && WorkerCombatPolicy.MustBeHomeNow(Game1.timeOfDay) && this.combatManager.EnsureHomeByMidnight(worker))
            {
                this.activePhases[workerId] = WorkerTravelPhase.RestingAtFarmhouse;
                this.fishingManager.Stop(workerId);
                this.ClearActiveJobState(workerId);
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId),
                    "Idle", "Home for the night", 0, null);
                continue;
            }

            WorkerTaskKind currentAssignment = this.workerShellManager.GetAssignedTask(workerId);
            WorkerStaminaState stamina = this.workerShellManager.GetWorkerStamina(workerId);
            bool stationary = worker.controller is null && !this.navigationManager.HasActiveRoute(workerId)
                && !this.workAnimations.IsActive(worker);
            bool waiting = this.activePhases.GetValueOrDefault(workerId) == WorkerTravelPhase.RestingAtFarmhouse
                || this.snapshots.GetValueOrDefault(workerId).State is "Idle" or "Waiting" or "Unavailable";
            this.workerShellManager.ObserveWorkerStamina(workerId,
                recovering: stationary && (currentAssignment == WorkerTaskKind.Idle || stamina.Resting || waiting));
            stamina = this.workerShellManager.GetWorkerStamina(workerId);
            bool withinHours = currentAssignment == WorkerTaskKind.Fish
                ? WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay) : WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay);
            if (stamina.Resting)
            {
                if (this.staminaRestingWorkers.Add(workerId))
                {
                    this.ClearActiveJobState(workerId);
                    this.navigationManager.StopTravel(worker);
                    this.idleMovement.Stop(workerId);
                    this.fishingManager.Stop(workerId);
                    this.activePhases[workerId] = WorkerTravelPhase.None;
                    this.workerShellManager.ObserveWorkerStamina(workerId, recovering: true);
                }
                if (withinHours)
                {
                    this.snapshots[workerId] = new(currentAssignment, "Resting",
                        $"Recovering stamina; resumes at {WorkerStaminaPolicy.ResumeThreshold(this.workerShellManager.GetWorkerStamina(workerId)):0}", 0, null);
                    continue;
                }
                if (this.activePhases.GetValueOrDefault(workerId) == WorkerTravelPhase.None)
                {
                    this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
                    continue;
                }
            }
            else if (this.staminaRestingWorkers.Remove(workerId) && withinHours)
            {
                this.activePhases[workerId] = WorkerTravelPhase.None;
                this.retryAfterTicks.Remove(workerId);
            }
            if (WorkerMiningPolicy.IsMiningTask(currentAssignment) && this.MayFollowDungeon(workerId)
                && this.workerShellManager.CanWorkerWorkToday(workerId)
                && this.PrepareMiningWork(worker, workerId, currentAssignment)) continue;

            if (this.workAnimations.IsActive(worker)) continue;

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

            if (phase == WorkerTravelPhase.RestingAtFarmhouse
                && this.workerShellManager.GetAssignedTask(workerId) == WorkerTaskKind.Idle
                && this.CanWanderToday(workerId)
                && WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
            {
                this.activePhases[workerId] = WorkerTravelPhase.None;
                phase = WorkerTravelPhase.None;
            }

            if (phase is WorkerTravelPhase.ReturningToFarmhouse or WorkerTravelPhase.RestingAtFarmhouse)
            {
                this.UpdateReturnHome(worker, workerId, phase);
                continue;
            }

            if (phase == WorkerTravelPhase.BlockedAtLocation)
                continue;

            WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
            if (assignment == WorkerTaskKind.Fish && !this.workerShellManager.CanWorkerWorkToday(workerId))
                this.fishingManager.Suspend(workerId);
            if (assignment == WorkerTaskKind.ExploreArea && !this.workerShellManager.CanWorkerWorkToday(workerId))
                this.explorationManager.Suspend(workerId);
            if (assignment != WorkerTaskKind.Idle && this.workerShellManager.CanWorkerWorkToday(workerId))
            {
                this.UpdateAssignedWork(worker, workerId, assignment);
                continue;
            }

            if (assignment == WorkerTaskKind.Idle)
            {
                if (!this.CanWanderToday(workerId) || !WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
                    this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
                else
                    this.snapshots[workerId] = this.idleMovement.Update(worker, workerId);
            }

        }
    }

    public void Reset()
    {
        this.staminaRestingWorkers.Clear();
        this.activePhases.Clear();
        this.idleMovement.Reset();
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
        this.explorationManager.Reset();
        this.fishingManager.Reset();
        this.workAnimations.Reset();
        this.miningTransitionRetry.Clear();
        this.miningLocations.Clear();
        this.miningModes.Clear();
    }

    public bool TryAssignTask(string workerId, WorkerTaskKind task, out string message)
        => this.TryAssignTask(workerId, task, out message, preserveDailyOrder: false);

    private bool TryAssignTask(string workerId, WorkerTaskKind task, out string message, bool preserveDailyOrder)
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
        if (task == WorkerTaskKind.Fish && Context.IsMainPlayer
            && WorkerFishingAreaCatalog.AccessReason(this.workerShellManager.GetFishingArea(workerId)) is string fishingReason)
        { message = fishingReason + "."; return false; }
        if (task == WorkerTaskKind.ExploreArea && Context.IsMainPlayer
            && WorkerExplorationManager.AccessReason(this.workerShellManager.GetExplorationArea(workerId)) is string reason)
        {
            message = reason + ".";
            return false;
        }
        if (WorkerMiningPolicy.IsMiningTask(task) && Context.IsMainPlayer
            && WorkerMiningAreaCatalog.AccessReason(this.workerShellManager.GetMiningArea(workerId)) is string miningReason)
        {
            message = miningReason + ".";
            return false;
        }
        if (!this.workerShellManager.TrySetAssignedTask(workerId, task, preserveDailyOrder))
        {
            message = "Only the host can assign worker tasks.";
            return false;
        }
        this.navigationManager.StopTravel(worker);
        this.idleMovement.Stop(workerId);
        this.staminaRestingWorkers.Remove(workerId);
        this.combatManager.Stop(workerId);
        this.explorationManager.Stop(workerId);
        this.fishingManager.Stop(workerId);
        if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Miner && task == WorkerTaskKind.Idle)
            this.dungeonTravel.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetMiningArea(workerId));
        else if (task == WorkerTaskKind.Idle)
            this.combatManager.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetCombatArea(workerId));
        if (!this.navigationManager.HasForeignController(worker))
        {
            this.navigationManager.StopWorker(worker);
        }
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.ClearActiveJobState(workerId);
        this.miningLocations.Remove(workerId);
        this.miningModes.Remove(workerId);
        this.miningTransitionRetry.Remove(workerId);
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
            if (worker.currentLocation is Farm && this.CanWanderToday(workerId)
                && WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(task, "Idle", "Looking around the Farm", 0, null);
            else
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
        this.staminaRestingWorkers.Remove(workerId);
        this.idleMovement.Stop(workerId);
        this.explorationManager.DeliverPendingLoot(workerId);
        this.fishingManager.Deliver(workerId);
        this.explorationManager.Stop(workerId);
        this.fishingManager.Stop(workerId);
        if (this.workerShellManager.TryGetWorker(workerId, out NPC? worker) && worker is not null)
            this.navigationManager.StopWorker(worker);
        this.ClearActiveJobState(workerId);
        this.miningLocations.Remove(workerId);
        this.miningModes.Remove(workerId);
        this.miningTransitionRetry.Remove(workerId);
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
        this.workAnimations.Stop(workerId);
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

        this.staminaRestingWorkers.Remove(workerId);
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
        this.staminaRestingWorkers.Remove(workerId);
        this.navigationManager.StopWorker(worker);
        this.combatManager.ExitGeneratedFloorForReturn(worker, previousArea);
        this.combatManager.Stop(workerId);
        this.explorationManager.Stop(workerId);
        this.fishingManager.Stop(workerId);
        this.ClearActiveJobState(workerId);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        return true;
    }

    public bool TrySetMiningArea(string workerId, string area, out string message)
    {
        if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        {
            message = $"Worker '{workerId}' was not found.";
            return false;
        }
        string previousArea = this.workerShellManager.GetMiningArea(workerId);
        if (!this.workerShellManager.TrySetMiningArea(workerId, area, out message)) return false;
        this.staminaRestingWorkers.Remove(workerId);
        this.dungeonTravel.ExitGeneratedFloorForReturn(worker, previousArea);
        this.navigationManager.StopWorker(worker);
        this.ClearActiveJobState(workerId);
        this.failedWorkDebris.Remove(workerId);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        return true;
    }

    public bool TrySetFishingArea(string workerId, string area, out string message)
    {
        if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        { message = $"Worker '{workerId}' was not found."; return false; }
        if (!this.workerShellManager.TrySetFishingArea(workerId, area, out message)) return false;
        this.staminaRestingWorkers.Remove(workerId);
        this.fishingManager.Stop(workerId);
        this.navigationManager.StopWorker(worker);
        this.ClearActiveJobState(workerId);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.BeginReturnHome(worker, workerId, ReturnHomeReason.ExplicitIdle);
        return true;
    }

    public bool TrySetExplorationArea(string workerId, string area, out string message)
    {
        if (!this.workerShellManager.TryGetWorker(workerId, out NPC? worker) || worker is null)
        {
            message = $"Worker '{workerId}' was not found.";
            return false;
        }
        if (!this.workerShellManager.TrySetExplorationArea(workerId, area, out message))
            return false;
        this.staminaRestingWorkers.Remove(workerId);
        this.navigationManager.StopWorker(worker);
        this.combatManager.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetCombatArea(workerId));
        this.combatManager.Stop(workerId);
        this.explorationManager.Stop(workerId);
        this.fishingManager.Stop(workerId);
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
        if (!Context.IsMainPlayer && current.AssignedTask == WorkerTaskKind.Idle)
        {
            this.workerShellManager.TryGetWorker(workerId, out NPC? idleWorker);
            if (idleWorker?.currentLocation is Farm)
                current = current with { State = idleWorker.controller is null ? "Idle" : "Traveling",
                    Status = idleWorker.controller is null ? "Looking around the Farm" : "Walking around the Farm" };
            else if (idleWorker?.currentLocation?.NameOrUniqueName == TestWorkerDefinition.LocationName)
                current = current with { State = "Idle", Status = "At home" };
        }
        WorkerExplorationProgress progress = this.workerShellManager.GetExplorationProgress(workerId);
        if (!Context.IsMainPlayer && current.AssignedTask == WorkerTaskKind.ExploreArea)
        {
            string area = this.workerShellManager.GetExplorationArea(workerId);
            this.workerShellManager.TryGetWorker(workerId, out NPC? worker);
            bool atEntrance = worker?.currentLocation?.NameOrUniqueName == WorkerExplorationAreaCatalog.Entrance(area);
            current = current with { State = atEntrance ? "Exploring" : "Idle", Status = atEntrance
                ? $"Exploring {WorkerExplorationAreaCatalog.GetLabel(area)}: {progress.Minutes}/60 minutes" : "Away from exploration entrance" };
        }
        WorkerFishingProgress fishing = this.workerShellManager.GetFishingProgress(workerId);
        if (!Context.IsMainPlayer && current.AssignedTask == WorkerTaskKind.Fish)
        {
            this.workerShellManager.TryGetWorker(workerId, out NPC? worker);
            bool casting = worker?.modData.ContainsKey(WorkerFishingManager.VisualKey) == true;
            current = current with { State = casting ? "Fishing" : "Idle", Status = casting
                ? $"Fishing at {WorkerFishingAreaCatalog.Label(this.workerShellManager.GetFishingArea(workerId))}: {fishing.Minutes}/{WorkerFishingPolicy.MinutesPerCatch} minutes"
                : "Away from fishing shore" };
        }
        bool withinStaminaWorkHours = current.AssignedTask == WorkerTaskKind.Fish
            ? WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay) : WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay);
        if (withinStaminaWorkHours && this.workerShellManager.GetWorkerStamina(workerId).Resting)
            current = current with { State = "Resting", Status = $"Recovering stamina; resumes at {WorkerStaminaPolicy.ResumeThreshold(this.workerShellManager.GetWorkerStamina(workerId)):0}" };
        return current with { CompletedToday = this.completedToday.GetValueOrDefault(workerId)
            + (fishing.Day == Game1.Date.TotalDays ? fishing.CompletedCatches : 0)
            + (progress.Day == Game1.Date.TotalDays ? progress.CompletedRuns : 0) };
    }

    public bool TryGetCombatHealth(string workerId, out int health, out int maxHealth)
        => this.combatManager.TryGetHealth(workerId, out health, out maxHealth);

    public void GetWorkerHealth(string workerId, out int health, out int maxHealth)
    {
        if (this.combatManager.TryGetHealth(workerId, out health, out maxHealth)) return;
        int maximum = this.workerShellManager.GetWorkerMaxHealth(workerId);
        if (this.workerShellManager.TryGetCombatHealthToday(workerId, out health, out maxHealth))
            health = WorkerCombatPolicy.NormalizeHealth(health, maxHealth, maximum);
        else
            health = maximum;
        maxHealth = maximum;
    }

    public void DrawCombatHealthBars(Microsoft.Xna.Framework.Graphics.SpriteBatch batch)
        => this.combatManager.DrawHealthBars(batch);

    public void DrawFishing(Microsoft.Xna.Framework.Graphics.SpriteBatch batch) => this.fishingManager.Draw(batch);

    public void RegisterCombatAnimations(HarmonyLib.Harmony harmony)
    {
        this.combatManager.RegisterAnimations(harmony);
        this.workAnimations.Register(harmony);
    }

    private void StartWorkMotion(NPC worker, string workerId, WorkerWorkAnimationKind kind, Point target,
        Action impact, bool requirePaidWork = true)
    {
        WorkerStaminaAction staminaAction = kind switch
        {
            WorkerWorkAnimationKind.Water => WorkerStaminaAction.Water,
            WorkerWorkAnimationKind.Axe => WorkerStaminaAction.Axe,
            WorkerWorkAnimationKind.Pickaxe => WorkerStaminaAction.Pickaxe,
            _ => WorkerStaminaAction.Free,
        };
        // Reserve no energy for windup: cancelled or invalid impacts spend nothing.
        if (!this.workerShellManager.TryUseWorkerStamina(workerId, staminaAction, spend: false)) return;
        WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
        WorkerProfession profession = this.workerShellManager.GetWorkerProfession(workerId);
        this.navigationManager.StopWorker(worker);
        this.workAnimations.Start(worker, kind, target,
            () => this.workerShellManager.GetAssignedTask(workerId) == assignment
                && (profession is not (WorkerProfession.CombatWorker or WorkerProfession.Miner) || !WorkerCombatPolicy.MustBeHomeNow(Game1.timeOfDay))
                && (!WorkerMiningPolicy.IsMiningTask(assignment) || !requirePaidWork || this.IsMiningImpactAllowed(worker, workerId, assignment))
                && (!requirePaidWork || (this.workerShellManager.CanWorkerWorkToday(workerId)
                    && WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay)))
                && !this.navigationManager.HasActiveRoute(workerId), impact);
    }

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
        if (reason == ReturnHomeReason.WorkComplete
            && WorkerTaskPolicy.ShouldIdleAfterWorkComplete(this.workerShellManager.GetAssignedTask(workerId),
                Game1.timeOfDay, this.workerShellManager.CanWorkerWorkToday(workerId),
                this.workerShellManager.GetWorkerStamina(workerId).Resting,
                this.workerShellManager.WasDefeatedToday(workerId))
            && this.TryAssignTask(workerId, WorkerTaskKind.Idle, out _, preserveDailyOrder: true))
        {
            // Use the exact manual Idle transition: persist the order, clear job state,
            // leave generated floors safely, and let daytime Farm wandering take over.
            this.LogState(workerId, worker, "work complete; switched to idle/return home");
            return;
        }

        this.idleMovement.Stop(workerId);
        if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Miner)
            this.dungeonTravel.ExitGeneratedFloorForReturn(worker, this.workerShellManager.GetMiningArea(workerId),
                () => this.ClearMiningFloorState(workerId));
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
        if (assignment == WorkerTaskKind.Fish)
        {
            if (this.fishingManager.Update(worker, workerId, out WorkerRuntimeSnapshot fishingSnapshot))
                this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            else this.snapshots[workerId] = fishingSnapshot;
            return;
        }
        if (assignment == WorkerTaskKind.ExploreArea)
        {
            if (this.explorationManager.Update(worker, workerId, out WorkerRuntimeSnapshot explorationSnapshot))
                this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            else
                this.snapshots[workerId] = explorationSnapshot;
            return;
        }
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
        if (IsAssignedObjectJob(assignment)
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
            if (action is WorkerTaskKind.WaterCrops or WorkerTaskKind.HarvestCrops)
            {
                Crop? expectedCrop = (feature as HoeDirt)?.crop;
                WorkerTaskKind selectedAction = action;
                WorkerWorkAnimationKind motion = action == WorkerTaskKind.WaterCrops ? WorkerWorkAnimationKind.Water
                    : expectedCrop?.GetHarvestMethod() == StardewValley.GameData.Crops.HarvestMethod.Scythe
                        ? WorkerWorkAnimationKind.Scythe : WorkerWorkAnimationKind.Gather;
                this.StartWorkMotion(worker, workerId, motion, target, () =>
                {
                    if (farm.terrainFeatures.TryGetValue(target.ToVector2(), out TerrainFeature? live)
                        && live is HoeDirt current && ReferenceEquals(current.crop, expectedCrop)
                        && WorkerTaskPolicy.SelectCropAction(assignment, !current.crop!.dead.Value,
                            IsHarvestable(current.crop), !current.isWatered()) == selectedAction
                        && this.PerformJobAt(workerId, farm, target, selectedAction))
                        this.RecordCompletedWork(workerId);
                    this.activeTargets.Remove(workerId);
                    this.LogState(workerId, worker, $"finished action at target tile {target}; rescanning farm");
                });
                return;
            }
            this.activeTargets.Remove(workerId);
            this.LogState(workerId, worker, $"skipped invalid action at target tile {target}; rescanning farm");
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

    private void ClearMiningFloorState(string workerId)
    {
        this.ClearActiveJobState(workerId);
        this.miningModes.Remove(workerId);
        this.miningLocations.Remove(workerId);
        this.failedWorkDebris.Remove(workerId);
        this.retryAfterTicks.Remove(workerId);
        this.miningTransitionRetry.Remove(workerId);
    }

    private WorkerMiningWorkMode ResolveMiningMode(string workerId, WorkerTaskKind assignment, GameLocation location)
    {
        if (assignment == WorkerTaskKind.MineRocks) return WorkerMiningWorkMode.AllStone;
        string area = this.workerShellManager.GetMiningArea(workerId);
        bool hasResources = assignment == WorkerTaskKind.MineOreGems && location.objects.Pairs.Any(pair =>
            WorkerMiningPolicy.ContainsTile(area, (int)pair.Key.X, (int)pair.Key.Y)
            && ((WorkerRouteObstacleClassifier.IsSmallLitter(pair.Value) && pair.Value.IsBreakableStone()
                    && WorkerMiningPolicy.IsOreGemOrCoal(pair.Value.ItemId))
                || WorkerMiningPolicy.CanCollectLooseGem(WorkerMiningWorkMode.Ores,
                    pair.Value.ItemId, pair.Value.IsBreakableStone())));
        if (hasResources) return WorkerMiningWorkMode.Ores;
        MineShaft? mine = location as MineShaft;
        return WorkerMiningPolicy.SelectMode(assignment, hasResources, mine is not null,
            mine is not null && WorkerMiningAreaCatalog.HasMineExit(mine),
            mine?.mustKillAllMonstersToAdvance() == true, mine?.shouldCreateLadderOnThisLevel() == true);
    }

    private bool IsMiningImpactAllowed(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        string area = this.workerShellManager.GetMiningArea(workerId);
        if (WorkerMiningAreaCatalog.AccessReason(area) is not null || worker.currentLocation is not GameLocation location)
            return false;
        if (WorkerMiningPolicy.IsDungeon(area) && (Game1.player.currentLocation != location
            || !WorkerDungeonTravelManager.IsMatchingActiveFloor(area, location))) return false;
        return this.ResolveMiningMode(workerId, assignment, location) != WorkerMiningWorkMode.None;
    }

    // True means this update is spent staging/waiting/returning rather than mining.
    private bool PrepareMiningWork(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (!WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
        {
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return true;
        }
        string area = this.workerShellManager.GetMiningArea(workerId);
        if (WorkerMiningAreaCatalog.AccessReason(area) is string reason)
        {
            this.ClearActiveJobState(workerId);
            this.snapshots[workerId] = new(assignment, "Unavailable", reason, 0, null);
            return true;
        }
        GameLocation? location;
        if (WorkerMiningPolicy.IsDungeon(area))
        {
            bool alreadyWorking = Game1.player.currentLocation == worker.currentLocation
                && WorkerDungeonTravelManager.IsMatchingActiveFloor(area, worker.currentLocation);
            if (alreadyWorking) location = worker.currentLocation;
            else
            {
                if (this.miningLocations.ContainsKey(workerId) && WorkerDungeonTravelManager.IsGeneratedFloor(worker.currentLocation))
                {
                    this.navigationManager.StopWorker(worker);
                    this.ClearMiningFloorState(workerId);
                }
                if (this.miningTransitionRetry.GetValueOrDefault(workerId) > Game1.ticks) return true;
                location = this.dungeonTravel.ResolveWorkLocation(area, worker, followStaged: true,
                    beforeTransition: () => this.ClearMiningFloorState(workerId));
                if (location is not null && worker.currentLocation != location
                    && !WorkerDungeonTravelManager.IsGeneratedFloor(worker.currentLocation))
                    this.dungeonTravel.TryFollowTransition(worker, location, nearFarmer: false,
                        beforeTransition: () => this.ClearMiningFloorState(workerId));
                this.miningTransitionRetry[workerId] = Game1.ticks + 120;
            }
            if (location is null || worker.currentLocation != location
                || !WorkerDungeonTravelManager.IsMatchingActiveFloor(area, location))
            {
                this.snapshots[workerId] = new(assignment, "Waiting",
                    $"Waiting by {WorkerMiningPolicy.GetLabel(area)} entrance; follow the host onto an active floor", 0, null);
                return true;
            }
        }
        else location = Game1.getLocationFromName(WorkerMiningPolicy.GetLocationName(area));
        if (location is null) return true;
        if (!this.miningLocations.TryGetValue(workerId, out GameLocation? previous) || previous != location)
        {
            this.ClearMiningFloorState(workerId);
            this.miningLocations[workerId] = location;
        }
        WorkerMiningWorkMode mode = this.ResolveMiningMode(workerId, assignment, location);
        if (this.miningModes.TryGetValue(workerId, out WorkerMiningWorkMode previousMode) && previousMode != mode)
        {
            this.navigationManager.StopWorker(worker);
            this.ClearActiveJobState(workerId);
        }
        this.miningModes[workerId] = mode;
        if (mode != WorkerMiningWorkMode.None) return false;
        this.navigationManager.StopTravel(worker);
        this.ClearActiveJobState(workerId);
        string message = area == WorkerMiningPolicy.Volcano
            ? "Volcano uses fixed exits, gates and lava crossings; waiting for the host to advance"
            : location is MineShaft mine
                ? mine.mustKillAllMonstersToAdvance() && !WorkerMiningAreaCatalog.HasMineExit(mine)
                    ? "Monsters must be defeated before vanilla opens an exit; waiting for the host"
                    : !mine.shouldCreateLadderOnThisLevel()
                        ? "This mine floor has no deeper ladder; waiting for the host"
                        : "Ladder or shaft is available; waiting for the host to descend"
                : "This area has no mine ladders";
        if (assignment == WorkerTaskKind.MineOreGems && !WorkerMiningPolicy.IsDungeon(area))
        {
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return true;
        }
        this.snapshots[workerId] = new(assignment, "Waiting", message, 0, null);
        return true;
    }

    private void FinishResourceSweep(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (!WorkerMiningPolicy.IsMiningTask(assignment)
            || !WorkerMiningPolicy.IsDungeon(this.workerShellManager.GetMiningArea(workerId)))
        {
            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }
        this.ClearActiveJobState(workerId);
        this.navigationManager.StopTravel(worker);
        this.retryAfterTicks[workerId] = Game1.ticks + 300;
        this.snapshots[workerId] = new(assignment, "Waiting", "No reachable rocks remain; waiting for the host or a clear route", 0, null);
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

        bool mining = WorkerMiningPolicy.IsMiningTask(assignment);
        string locationName = mining
            ? this.miningLocations.GetValueOrDefault(workerId)?.NameOrUniqueName
                ?? WorkerMiningPolicy.GetLocationName(this.workerShellManager.GetMiningArea(workerId))
            : assignment == WorkerTaskKind.ClearDebris
                && this.workerShellManager.GetWorkerProfession(workerId) != WorkerProfession.Forager
                ? "Farm" : this.workerShellManager.GetForageLocationName(workerId);
        GameLocation? location = mining ? this.miningLocations.GetValueOrDefault(workerId) : Game1.getLocationFromName(locationName);
        if (location is null || (!mining && !WorkerForageAreaCatalog.IsValidLocation(locationName)))
        {
            this.FinishResourceSweep(worker, workerId, assignment);
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
                if (WorkerMiningPolicy.IsMiningTask(assignment))
                {
                    HashSet<Point> failed = this.failedWorkDebris.GetValueOrDefault(workerId) ?? new();
                    this.failedWorkDebris[workerId] = failed;
                    failed.Add(active.ResourceTile);
                    this.foragerSearches.Remove(workerId);
                }
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
                    this.FinishResourceSweep(worker, workerId, assignment);
                return;
            }

            search = new ForagerSearchState
            {
                TargetLocation = location,
                OriginLocation = worker.currentLocation!,
                OriginTile = worker.TilePoint,
                Assignment = assignment,
                Candidates = worker.currentLocation == location
                    ? candidates.OrderBy(target => mining && this.miningModes.GetValueOrDefault(workerId) == WorkerMiningWorkMode.FindLadder
                        && target.ExpectedObject is not null && WorkerMiningPolicy.IsOreGemOrCoal(target.ExpectedObject.ItemId) ? 1 : 0)
                        .ThenBy(target => Math.Abs(worker.TilePoint.X - target.ApproachTile.X)
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
            if (mining && (worker.currentLocation != location || (WorkerMiningPolicy.IsDungeon(this.workerShellManager.GetMiningArea(workerId))
                && (Game1.player.currentLocation != location || !WorkerDungeonTravelManager.IsActiveFloor(location))))) return false;
            if (IsAssignedObjectJob(assignment)
                && (this.IsDebrisReservedByAnother(workerId, search.TargetLocation, target.ResourceTile)
                    || !search.TargetLocation.objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? current)
                    || !ReferenceEquals(current, target.ExpectedObject)
                    || !this.IsEligibleAssignedObject(workerId, assignment, target.ResourceTile, current)))
                return false;

            WorkerNavigationTarget navigationTarget = new(target.LocationName, target.ApproachTile, TestWorkerDefinition.FacingDirection);
            HashSet<Point> validApproaches = search.Candidates
                .Where(candidate => candidate.ResourceTile == target.ResourceTile && candidate.Kind == target.Kind)
                .Select(candidate => candidate.ApproachTile)
                .ToHashSet();
            if (!this.navigationManager.TryStartTravel(worker, navigationTarget,
                    $"travel to {WorkerTaskPolicy.GetTaskLabel(assignment).ToLowerInvariant()} target",
                    out WorkerNavigationTarget candidateTarget, validApproaches.Contains,
                    actual => this.activeForagerTargets[workerId] = target with { ApproachTile = actual.Tile },
                    stepValidator: mining ? step => this.dungeonTravel.IsSafeRouteStep(location, step) : null))
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
                this.FinishResourceSweep(worker, workerId, assignment);
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
        search.ObstaclePlanner = this.navigationManager.CreateObstacleRoutePlanner(worker, potentialApproaches, excludedTiles,
            WorkerMiningPolicy.IsMiningTask(assignment) ? tile => this.dungeonTravel.IsSafeRouteStep(search.TargetLocation, tile) : null);
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
            && (!IsAssignedObjectJob(assignment)
                || !this.IsDebrisReservedByAnother(workerId, search.TargetLocation, clearance.ObstacleTile))
            && this.navigationManager.IsSmallRouteObstacle(worker, clearance.ObstacleTile))
        {
            WorkerNavigationTarget approach = new(search.TargetLocation.NameOrUniqueName,
                clearance.ApproachTile, TestWorkerDefinition.FacingDirection);
            if (this.navigationManager.TryStartTravel(worker, approach, "walk to route debris", out _,
                    stepValidator: WorkerMiningPolicy.IsMiningTask(assignment)
                        ? step => this.dungeonTravel.IsSafeRouteStep(search.TargetLocation, step) : null))
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
                    IsAssignedDebrisJob = this.IsEligibleAssignedObject(workerId, assignment, clearance.ObstacleTile, item),
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
                    actual => this.activeForagerTargets[workerId] = currentCandidate with { ApproachTile = actual.Tile },
                    stepValidator: WorkerMiningPolicy.IsMiningTask(assignment)
                        ? step => this.dungeonTravel.IsSafeRouteStep(search.TargetLocation, step) : null))
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

        this.monitor.Log($"{worker.displayName} could not reach {assignment} work in {search.TargetLocation.NameOrUniqueName}; route obstacle check={status}. Ending this resource search.", LogLevel.Info);
        this.FinishResourceSweep(worker, workerId, assignment);
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
        if ((clear.IsAssignedDebrisJob || (WorkerMiningPolicy.IsMiningTask(assignment) && !clear.ContinueReturnHome))
            && (!IsAssignedObjectJob(assignment)
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
            ? !this.IsEligibleAssignedObject(workerId, assignment, clear.ObstacleTile, item)
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

        // Vanilla only advances this visual/tool cooldown in the host's current
        // location. Quarry work must also finish when the host is elsewhere.
        if (WorkerMiningPolicy.IsMiningTask(assignment) && clear.Location != Game1.currentLocation && item.shakeTimer > 0)
            item.shakeTimer = Math.Max(0, item.shakeTimer - (int)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds);

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
            : new Pickaxe { UpgradeLevel = WorkerMiningPolicy.IsMiningTask(assignment)
                ? WorkerMiningPolicy.PickaxeUpgradeLevel : 0 };
        clear.NextSwingTick = Game1.ticks + ForagerSwingIntervalTicks;
        this.StartWorkMotion(worker, workerId, item.IsWeeds() ? WorkerWorkAnimationKind.Scythe
            : item.IsTwig() ? WorkerWorkAnimationKind.Axe : WorkerWorkAnimationKind.Pickaxe, clear.ObstacleTile,
            () => this.PerformObstacleTool(worker, workerId, assignment, clear, tool), requirePaidWork: clear.IsAssignedDebrisJob
                || (WorkerMiningPolicy.IsMiningTask(assignment) && !clear.ContinueReturnHome));
    }

    private void PerformObstacleTool(NPC worker, string workerId, WorkerTaskKind assignment, ActiveObstacleClear clear, Tool tool)
    {
        if (!this.activeObstacleClears.TryGetValue(workerId, out ActiveObstacleClear? current) || current != clear
            || !clear.Location.objects.TryGetValue(clear.ObstacleTile.ToVector2(), out StardewValley.Object? item)
            || !ReferenceEquals(item, clear.ExpectedObject) || item.shakeTimer > 0
            || Math.Abs(worker.TilePoint.X - clear.ObstacleTile.X) + Math.Abs(worker.TilePoint.Y - clear.ObstacleTile.Y) != 1)
            return;
        if (clear.IsAssignedDebrisJob && !this.IsEligibleAssignedObject(workerId, assignment, clear.ObstacleTile, item))
            return;
        if (!this.workerShellManager.TryUseWorkerStamina(workerId, tool is Axe ? WorkerStaminaAction.Axe
                : tool is Pickaxe ? WorkerStaminaAction.Pickaxe : WorkerStaminaAction.Free)) return;
        tool.lastUser = Game1.MasterPlayer;
        clear.ToolActionCount++;
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
        if (WorkerMiningPolicy.IsMiningTask(assignment) && item.IsBreakableStone()
            && actionStatus is (WorkerObstacleToolActionStatus.Continue or WorkerObstacleToolActionStatus.Completed))
            clear.Location.playSound(actionStatus == WorkerObstacleToolActionStatus.Completed ? "stoneCrack" : "hammer",
                clear.ObstacleTile.ToVector2());
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
                workerId, worker.displayName, assignment, ForagerTargetKind.RouteDebris,
                this.workerShellManager.GetHarvestDestination(workerId), clear.CapturedDebris));

        this.monitor.Log($"{worker.displayName} cleared small route debris at {clear.Location.NameOrUniqueName} {clear.ObstacleTile}; captured {clear.CapturedDebris.Count} drop groups.", LogLevel.Info);
        if (clear.IsAssignedDebrisJob)
        {
            this.RecordCompletedWork(workerId);
            this.workerShellManager.TryAwardCompletedActionExperience(workerId,
                item.IsBreakableStone() ? WorkerExperienceAction.ClearStone
                : item.IsTwig() ? WorkerExperienceAction.ClearTwig : WorkerExperienceAction.ClearWeeds);
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Working",
                $"{(WorkerMiningPolicy.IsMiningTask(assignment) ? "Mined rock" : "Cleared debris")} at {clear.ObstacleTile}", 0, clear.ObstacleTile);
        }
        this.activeObstacleClears.Remove(workerId);
        this.foragerSearches.Remove(workerId);
        this.navigationManager.ApplyIdlePose(worker);
        if (clear.ContinueReturnHome)
            this.workAnimations.AfterCompletion(worker, () => this.BeginReturnHome(worker, workerId, clear.ReturnReason));
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
                ForagerTargetKind.RouteDebris, this.workerShellManager.GetHarvestDestination(workerId), clear.CapturedDebris));

        HashSet<Point> failed = this.failedWorkDebris.GetValueOrDefault(workerId) ?? new HashSet<Point>();
        this.failedWorkDebris[workerId] = failed;
        failed.Add(clear.ObstacleTile);
        if (failed.Count >= MaxFailedWorkDebrisApproaches
            && !IsAssignedObjectJob(this.workerShellManager.GetAssignedTask(workerId)))
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

    private static bool IsAssignedObjectJob(WorkerTaskKind assignment)
        => assignment == WorkerTaskKind.ClearDebris || WorkerMiningPolicy.IsMiningTask(assignment);

    private bool IsEligibleAssignedObject(string workerId, WorkerTaskKind assignment, Point tile, StardewValley.Object? item)
    {
        if (!IsAssignedObjectJob(assignment) || item is null) return false;
        if (assignment == WorkerTaskKind.ClearDebris) return WorkerRouteObstacleClassifier.IsSmallLitter(item);
        if (!WorkerMiningPolicy.ContainsTile(this.workerShellManager.GetMiningArea(workerId), tile.X, tile.Y))
            return false;
        WorkerMiningWorkMode mode = this.miningModes.GetValueOrDefault(workerId);
        return (WorkerRouteObstacleClassifier.IsSmallLitter(item) && item.IsBreakableStone()
                && WorkerMiningPolicy.CanTarget(mode, item.ItemId))
            || (assignment == WorkerTaskKind.MineOreGems
                && WorkerMiningPolicy.CanCollectLooseGem(mode, item.ItemId, item.IsBreakableStone()));
    }

    private IEnumerable<ForagerTarget> GetForagerTargets(NPC worker, string workerId, GameLocation location,
        WorkerTaskKind assignment, bool includeBlockedApproaches = false)
    {
        if (IsAssignedObjectJob(assignment))
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                if (!this.IsEligibleAssignedObject(workerId, assignment, pair.Key.ToPoint(), pair.Value))
                    continue;

                Point debrisTile = pair.Key.ToPoint();
                if (this.failedWorkDebris.GetValueOrDefault(workerId)?.Contains(debrisTile) == true
                    || this.IsDebrisReservedByAnother(workerId, location, debrisTile))
                    continue;

                ForagerTargetKind kind = WorkerMiningPolicy.IsMiningTask(assignment) && !pair.Value.IsBreakableStone()
                    ? ForagerTargetKind.LooseGem : ForagerTargetKind.RouteDebris;

                foreach (Point approach in this.GetApproachTiles(worker, location, debrisTile, 1, 1, includeBlockedApproaches))
                {
                    if (Math.Abs(approach.X - debrisTile.X) + Math.Abs(approach.Y - debrisTile.Y) == 1)
                        yield return new ForagerTarget(location.NameOrUniqueName, debrisTile, approach,
                            kind, pair.Value);
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
                && pair.Value.Kind is (ForagerTargetKind.RouteDebris or ForagerTargetKind.LooseGem)
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
            : this.navigationManager.IsWalkableWorkTile(location, worker, tile)
                && (!WorkerDungeonTravelManager.IsGeneratedFloor(location) || this.dungeonTravel.IsSafeRouteStep(location, tile)));

    private void BeginForagerAction(
        NPC worker,
        string workerId,
        GameLocation location,
        ForagerTarget target,
        WorkerTaskKind assignment)
    {
        if (IsAssignedObjectJob(assignment) && target.Kind == ForagerTargetKind.RouteDebris)
        {
            if (!location.objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? item)
                || !ReferenceEquals(item, target.ExpectedObject)
                || !this.IsEligibleAssignedObject(workerId, assignment, target.ResourceTile, item)
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
                    item.MinutesUntilReady, damagePerToolAction: WorkerMiningPolicy.IsMiningTask(assignment)
                        ? WorkerMiningPolicy.PickaxeDamage : 1, hardLimit: MaxDebrisToolActions),
                IsAssignedDebrisJob = true,
            };
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, WorkerMiningPolicy.IsMiningTask(assignment) ? "Mining" : "Clearing",
                $"{(WorkerMiningPolicy.IsMiningTask(assignment) ? this.miningModes.GetValueOrDefault(workerId) == WorkerMiningWorkMode.FindLadder ? "Breaking stone to find a ladder" : "Mining rocks" : "Clearing debris")} in {WorkerForageAreaCatalog.GetDisplayName(location.NameOrUniqueName)}", 0, target.ResourceTile);
            this.monitor.Log($"{worker.displayName} started clearing assigned debris at {location.NameOrUniqueName} {target.ResourceTile} from {target.ApproachTile}.", LogLevel.Info);
            return;
        }

        if (target.Kind == ForagerTargetKind.Forage)
        {
            StardewValley.Object? expectedForage = location.Objects.GetValueOrDefault(target.ResourceTile.ToVector2());
            this.StartWorkMotion(worker, workerId, WorkerWorkAnimationKind.Gather, target.ResourceTile, () =>
            {
                if (location.Objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? forage)
                    && ReferenceEquals(forage, expectedForage) && forage.IsSpawnedObject && forage.isForage())
                {
                    location.Objects.Remove(target.ResourceTile.ToVector2());
                    bool chestOnly = WorkerItemStorage.Store(forage, this.workerShellManager.GetHarvestDestination(workerId), this.monitor);
                    this.RecordCompletedWork(workerId);
                    this.workerShellManager.TryAwardCompletedActionExperience(workerId, WorkerExperienceAction.PickupForage);
                    this.monitor.Log(
                        $"Worker {worker.displayName} [{workerId}] {assignment} forage at {target.LocationName} {target.ResourceTile}: collected; "
                        + $"storage={(chestOnly ? "selected chest" : "shipping bin, wholly or partly")}.",
                        LogLevel.Info);
                    location.playSound("pickUpItem");
                }

                this.FinishForagerAction(worker, workerId, target, assignment, null);
            });
            return;
        }

        if (target.Kind == ForagerTargetKind.LooseGem)
        {
            this.snapshots[workerId] = new(assignment, "Gathering", "Picking up a loose mineral", 0, target.ResourceTile);
            this.StartWorkMotion(worker, workerId, WorkerWorkAnimationKind.Gather, target.ResourceTile, () =>
            {
                if (worker.currentLocation == location && worker.TilePoint == target.ApproachTile
                    && location.Objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? gem)
                    && ReferenceEquals(gem, target.ExpectedObject)
                    && this.IsEligibleAssignedObject(workerId, assignment, target.ResourceTile, gem))
                {
                    try
                    {
                        bool chestOnly = WorkerItemStorage.Store(gem, this.workerShellManager.GetHarvestDestination(workerId), this.monitor);
                        location.Objects.Remove(target.ResourceTile.ToVector2());
                        this.RecordCompletedWork(workerId);
                        this.workerShellManager.TryAwardCompletedActionExperience(workerId, WorkerExperienceAction.PickupLooseGem);
                        this.monitor.Log($"{worker.displayName} gathered {gem.DisplayName} at {target.LocationName} {target.ResourceTile}; "
                            + $"storage={(chestOnly ? "selected chest" : "shipping bin, wholly or partly")}.", LogLevel.Info);
                        location.playSound("pickUpItem");
                    }
                    catch (Exception ex)
                    {
                        this.monitor.Log($"{worker.displayName} could not store loose gem at {target.LocationName} {target.ResourceTile}: {ex.Message}", LogLevel.Warn);
                    }
                }
                this.FinishForagerAction(worker, workerId, target, assignment, null);
            });
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

        if (Game1.ticks < action.NextSwingTick) return;
        action.NextSwingTick = Game1.ticks + ForagerSwingIntervalTicks;
        this.StartWorkMotion(worker, workerId, WorkerWorkAnimationKind.Axe, action.Target.ResourceTile,
            () => this.PerformForagerSwing(worker, workerId, action));
    }

    private void PerformForagerSwing(NPC worker, string workerId, ActiveForagerAction action)
    {
        GameLocation? location = Game1.getLocationFromName(action.Target.LocationName);
        if (location is null || worker.currentLocation != location || worker.TilePoint != action.Target.ApproachTile
            || !this.activeForagerActions.TryGetValue(workerId, out ActiveForagerAction? current) || current != action)
            return;
        bool resourcePresent = action.Target.Kind == ForagerTargetKind.HardwoodClump
            ? location.resourceClumps.Any(clump => clump.Tile.ToPoint() == action.Target.ResourceTile
                && clump.parentSheetIndex.Value is ResourceClump.stumpIndex or ResourceClump.hollowLogIndex)
            : location.terrainFeatures.TryGetValue(action.Target.ResourceTile.ToVector2(), out TerrainFeature? feature)
                && feature is Tree tree && !tree.falling.Value
                && (IsHardwoodTree(tree) == (action.Assignment == WorkerTaskKind.ChopHardwood));
        if (resourcePresent && !this.workerShellManager.TryUseWorkerStamina(workerId, WorkerStaminaAction.Axe)) return;
        action.SwingCount++;
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
                target.Kind,
                this.workerShellManager.GetHarvestDestination(workerId)));
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
                    if (WorkerMiningPolicy.IsMiningTask(zone.Assignment) && WorkerMiningPolicy.LeaveDropForPlayer(item!.QualifiedItemId))
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
                        chestOnly = WorkerItemStorage.Store(item!, zone.Destination, this.monitor);
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
            if (!this.workerShellManager.TryUseWorkerStamina(workerId, WorkerStaminaAction.Water)) return false;
            dirt.state.Value = HoeDirt.watered;
            if (farm == Game1.currentLocation) farm.localSound("wateringCan");
            this.monitor.Log($"Worker watered crop tile {tile}.", LogLevel.Trace);
            return true;
        }
        if (task == WorkerTaskKind.HarvestCrops && IsHarvestable(dirt.crop))
        {
            Crop crop = dirt.crop;
            int regrowDaysBefore = crop.dayOfCurrentPhase.Value;
            this.monitor.Log($"Worker reached crop tile {tile}; harvesting crop {crop.indexOfHarvest.Value} (regrows={crop.RegrowsAfterHarvest()}).", LogLevel.Trace);
            WorkerHarvestCollector collector = new(farm, tile, this.workerShellManager.GetHarvestDestination(workerId), this.monitor);
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
