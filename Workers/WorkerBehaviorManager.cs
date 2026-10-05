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
    }

    private sealed record ForagerTarget(string LocationName, Point ResourceTile, Point ApproachTile, ForagerTargetKind Kind);

    private sealed record DropCollectionZone(
        GameLocation Location, Point Tile, HashSet<Debris> ExistingDebris, int CollectAfterTick, int ExpiresAtTick,
        string WorkerId, string WorkerName, WorkerTaskKind Assignment, ForagerTargetKind Kind)
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

    private const int TravelRetryCooldownTicks = 60;
    private const int BlockedReturnRetryCooldownTicks = 600;
    private const int MaxReturnHomeRouteAttemptsPerUpdate = 4;
    private const int ReturnHomePlanningBudgetMilliseconds = 100;
    private const int MaxForagerRouteAttemptsPerUpdate = 4;
    private const int ForagerPlanningBudgetMilliseconds = 50;
    private const int ForagerSearchRetryTicks = 20;
    private const int ForagerSwingIntervalTicks = 24;
    private readonly Dictionary<string, WorkerTravelPhase> activePhases = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly WorkerNavigationManager navigationManager;
    private readonly WorkerShellManager workerShellManager;
    private readonly Dictionary<string, WorkerRuntimeSnapshot> snapshots = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> completedToday = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> activeTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ForagerTarget> activeForagerTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActiveForagerAction> activeForagerActions = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<DropCollectionZone> dropCollectionZones = new();
    private readonly Dictionary<string, WorkerNavigationTarget> returnTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReturnHomeReason> returnReasons = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> retryAfterTicks = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> nextReturnCandidateIndex = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> nextForagerCandidateIndex = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> lastDebugStates = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Point, string> lastHarvestReadinessStates = new();
    private readonly HashSet<Point> blockedHarvestTiles = new();

    public WorkerBehaviorManager(WorkerNavigationManager navigationManager, WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.navigationManager = navigationManager;
        this.workerShellManager = workerShellManager;
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

        this.UpdateDropCollectionZones();

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
        {
            if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
            {
                continue;
            }

            WorkerTravelPhase phase = this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None);
            if (phase is WorkerTravelPhase.ReturningToFarmhouse or WorkerTravelPhase.RestingAtFarmhouse)
            {
                this.UpdateReturnHome(worker, workerId, phase);
                continue;
            }

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
        this.dropCollectionZones.Clear();
        this.returnTargets.Clear();
        this.returnReasons.Clear();
        this.retryAfterTicks.Clear();
        this.nextReturnCandidateIndex.Clear();
        this.nextForagerCandidateIndex.Clear();
        this.lastDebugStates.Clear();
        this.lastHarvestReadinessStates.Clear();
        this.blockedHarvestTiles.Clear();
        this.navigationManager.Reset();
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
        this.nextForagerCandidateIndex.Remove(workerId);
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
        this.nextForagerCandidateIndex.Remove(workerId);
        this.activePhases.Remove(workerId);
        this.completedToday.Remove(workerId);
    }

    private void ClearActiveJobState(string workerId)
    {
        this.activeTargets.Remove(workerId);
        this.activeForagerTargets.Remove(workerId);
        this.activeForagerActions.Remove(workerId);
        this.nextForagerCandidateIndex.Remove(workerId);
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

    public WorkerRuntimeSnapshot GetRuntimeSnapshot(string workerId)
    {
        WorkerRuntimeSnapshot current = this.snapshots.TryGetValue(workerId, out WorkerRuntimeSnapshot snapshot)
            ? snapshot
            : new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId), "Idle", "Waiting for an assignment", 0, null);
        return current with { CompletedToday = this.completedToday.GetValueOrDefault(workerId) };
    }

    private void RecordCompletedWork(string workerId)
    {
        this.completedToday[workerId] = this.completedToday.GetValueOrDefault(workerId) + 1;
    }

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
            this.retryAfterTicks[workerId] = Game1.ticks + BlockedReturnRetryCooldownTicks;
            this.LogState(workerId, worker, $"return-home route retry failed for {homeTarget.Tile}; waiting before retry");
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
        this.retryAfterTicks[workerId] = Game1.ticks + BlockedReturnRetryCooldownTicks;

        this.snapshots[workerId] = new WorkerRuntimeSnapshot(
            this.workerShellManager.GetAssignedTask(workerId),
            "Returning",
            "Returning home",
            0,
            null);
        this.LogState(workerId, worker, "home route unavailable; waiting before retry");
    }

    private bool CanRetryTravel(string workerId)
    {
        return !this.retryAfterTicks.TryGetValue(workerId, out int retryAfter) || Game1.ticks >= retryAfter;
    }

    private void UpdateAssignedWork(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Forager)
        {
            this.UpdateForagerWork(worker, workerId, assignment);
            return;
        }

        if (!WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay) || worker.controller is not null)
            return;
        Farm farm = Game1.getFarm();
        if (worker.currentLocation == farm
            && this.activeTargets.TryGetValue(workerId, out Point target)
            && worker.TilePoint == target && worker.controller is null)
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
            if (this.PerformJobAt(farm, target, action))
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
            return;

        if (this.activeForagerActions.TryGetValue(workerId, out ActiveForagerAction? action))
        {
            this.UpdateForagerAction(worker, workerId, action);
            return;
        }

        if (worker.controller is not null)
            return;

        string locationName = this.workerShellManager.GetForageLocationName(workerId);
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

        List<ForagerTarget> candidates = this.GetForagerTargets(location, assignment).Take(96).ToList();
        if (candidates.Count == 0)
        {
            this.nextForagerCandidateIndex.Remove(workerId);
            if (this.HasFallingTree(location, assignment))
            {
                this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Working", "Waiting for a felled tree to settle", 0, null);
                return;
            }

            this.BeginReturnHome(worker, workerId, ReturnHomeReason.WorkComplete);
            return;
        }

        // A full path search for every candidate stalls the game when a tree falls.
        // Cheap distance orders candidates; TryStartTravel checks the actual route.
        List<ForagerTarget> ordered = worker.currentLocation == location
            ? candidates.OrderBy(target => Math.Abs(worker.TilePoint.X - target.ApproachTile.X)
                + Math.Abs(worker.TilePoint.Y - target.ApproachTile.Y)).ToList()
            : candidates.OrderBy(target => target.ResourceTile.X + target.ResourceTile.Y).ToList();
        int startIndex = this.nextForagerCandidateIndex.GetValueOrDefault(workerId) % ordered.Count;
        WorkerNavigationTarget? resolvedTarget = null;
        bool TryCandidate(ForagerTarget target)
        {
            WorkerNavigationTarget navigationTarget = new(target.LocationName, target.ApproachTile, TestWorkerDefinition.FacingDirection);
            HashSet<Point> validApproaches = candidates
                .Where(candidate => candidate.ResourceTile == target.ResourceTile && candidate.Kind == target.Kind)
                .Select(candidate => candidate.ApproachTile)
                .ToHashSet();
            if (!this.navigationManager.TryStartTravel(worker, navigationTarget,
                    $"travel to {WorkerTaskPolicy.GetTaskLabel(assignment).ToLowerInvariant()} target",
                    out WorkerNavigationTarget candidateTarget, validApproaches.Contains,
                    actual => this.activeForagerTargets[workerId] = target with { ApproachTile = actual.Tile }))
                return false;

            resolvedTarget = candidateTarget;
            return true;
        }

        bool foundRoute = WorkerBoundedCandidateSearch.TryFind(ordered, startIndex,
            MaxForagerRouteAttemptsPerUpdate, ForagerPlanningBudgetMilliseconds, TryCandidate,
            out ForagerTarget? selectedTarget, out int nextIndex, out int attempted, out long planningMilliseconds);
        if (planningMilliseconds >= 100)
            this.monitor.Log($"Forager route search took {planningMilliseconds} ms for {attempted} candidates in {location.NameOrUniqueName}.", LogLevel.Info);
        if (foundRoute && selectedTarget is not null && resolvedTarget is not null)
        {
            ForagerTarget target = selectedTarget;

            this.activeForagerTargets[workerId] = target with { ApproachTile = resolvedTarget.Tile };
            this.nextForagerCandidateIndex.Remove(workerId);
            this.activePhases[workerId] = WorkerTravelPhase.TravellingToFarmTarget;
            this.retryAfterTicks.Remove(workerId);
            this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Traveling",
                $"Walking to {WorkerForageAreaCatalog.GetDisplayName(locationName)} {target.ResourceTile}", 0, target.ResourceTile);
            this.LogState(workerId, worker, $"selected {assignment} target {locationName} {target.ResourceTile} via {target.ApproachTile}");
            return;
        }

        this.nextForagerCandidateIndex[workerId] = nextIndex;
        this.retryAfterTicks[workerId] = Game1.ticks
            + (startIndex + attempted >= ordered.Count ? TravelRetryCooldownTicks : ForagerSearchRetryTicks);
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Blocked", "No reachable target found; retrying", 0, null);
    }

    private IEnumerable<ForagerTarget> GetForagerTargets(GameLocation location, WorkerTaskKind assignment)
    {
        if (assignment == WorkerTaskKind.CollectForage)
        {
            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                if (!pair.Value.IsSpawnedObject || !pair.Value.isForage())
                    continue;

                Point resourceTile = pair.Key.ToPoint();
                foreach (Point approach in this.GetApproachTiles(location, resourceTile, 1, 1))
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
            foreach (Point approach in this.GetApproachTiles(location, resourceTile, 1, 1))
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
            foreach (Point approach in this.GetApproachTiles(location, resourceTile, clump.width.Value, clump.height.Value))
                yield return new ForagerTarget(location.NameOrUniqueName, resourceTile, approach, ForagerTargetKind.HardwoodClump);
        }
    }

    private IEnumerable<Point> GetApproachTiles(GameLocation location, Point resourceTile, int width, int height)
    {
        HashSet<Point> yielded = new();
        for (int x = resourceTile.X - 1; x <= resourceTile.X + width; x++)
        {
            foreach (int y in new[] { resourceTile.Y - 1, resourceTile.Y + height })
            {
                Point candidate = new(x, y);
                if (yielded.Add(candidate) && this.IsWalkableApproach(location, candidate))
                    yield return candidate;
            }
        }
        for (int y = resourceTile.Y; y < resourceTile.Y + height; y++)
        {
            foreach (int x in new[] { resourceTile.X - 1, resourceTile.X + width })
            {
                Point candidate = new(x, y);
                if (yielded.Add(candidate) && this.IsWalkableApproach(location, candidate))
                    yield return candidate;
            }
        }
    }

    private bool IsWalkableApproach(GameLocation location, Point tile)
    {
        return location.isTileOnMap(tile.ToVector2())
            && !location.IsTileBlockedBy(tile.ToVector2(), CollisionMask.All, CollisionMask.Characters, useFarmerTile: true);
    }

    private void BeginForagerAction(
        NPC worker,
        string workerId,
        GameLocation location,
        ForagerTarget target,
        WorkerTaskKind assignment)
    {
        if (target.Kind == ForagerTargetKind.Forage)
        {
            if (location.Objects.TryGetValue(target.ResourceTile.ToVector2(), out StardewValley.Object? forage)
                && forage.IsSpawnedObject && forage.isForage())
            {
                location.Objects.Remove(target.ResourceTile.ToVector2());
                bool chestOnly = WorkerItemStorage.Store(forage, this.workerShellManager.GetHarvestDestination(), this.monitor);
                this.RecordCompletedWork(workerId);
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
        bool finished = action.Target.Kind == ForagerTargetKind.HardwoodClump
            ? this.SwingAtHardwoodClump(location, action.Target, axe)
            : this.SwingAtTree(location, action.Target, action.Assignment, axe);

        this.monitor.Log(
            $"{worker.displayName} performed {action.Assignment} swing {action.SwingCount} at {action.Target.LocationName} {action.Target.ResourceTile}; finished={finished}.",
            LogLevel.Trace);
        if (finished)
        {
            if (resourcePresent)
                this.RecordCompletedWork(workerId);
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
                    if (zone.ExistingDebris.Contains(debris) || !this.IsDebrisNear(debris, zone.Tile, 9) || !this.TryCreateDebrisItem(debris, out Item? item))
                        continue;

                    if (!zone.StorageStartedLogged)
                    {
                        this.monitor.Log(
                            $"Worker {zone.WorkerName} [{zone.WorkerId}] {zone.Assignment} {zone.Kind} at "
                            + $"{zone.Location.NameOrUniqueName} {zone.Tile}: storing first nearby drop.",
                            LogLevel.Info);
                        zone.StorageStartedLogged = true;
                    }
                    zone.Location.debris.RemoveAt(debrisIndex);
                    System.Diagnostics.Stopwatch storageTime = System.Diagnostics.Stopwatch.StartNew();
                    bool chestOnly = WorkerItemStorage.Store(item!, this.workerShellManager.GetHarvestDestination(), this.monitor);
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
            this.monitor.Log(
                $"No collision-aware route length was available for {desiredAction}; falling back to nearest eligible tile {target}.",
                LogLevel.Trace);
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
        if (!this.navigationManager.TryStartTravel(worker, new WorkerNavigationTarget("Farm", target, 2), reason,
                out WorkerNavigationTarget resolvedTarget,
                landingValidator: point => this.IsEligibleCropTarget(workerId, farm, point, assignment, task),
                onWarpArrival: actual => this.activeTargets[workerId] = actual.Tile))
        {
            this.LogState(workerId, worker, $"route start failed for target tile {target}; clearing target and waiting for next update");
            this.activeTargets.Remove(workerId);
            this.retryAfterTicks[workerId] = Game1.ticks + TravelRetryCooldownTicks;
            return;
        }

        this.activeTargets[workerId] = resolvedTarget.Tile;
        this.retryAfterTicks.Remove(workerId);
    }

    private bool PerformJobAt(Farm farm, Point tile, WorkerTaskKind task)
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
            this.monitor.Log($"Worker reached crop tile {tile}; harvesting crop {crop.indexOfHarvest.Value} (regrows={crop.RegrowsAfterHarvest()}).", LogLevel.Trace);
            WorkerHarvestCollector collector = new(farm, tile, this.workerShellManager.GetHarvestDestination(), this.monitor);
            int shippingBinCountBefore = farm.getShippingBin(Game1.MasterPlayer).Count;
            bool harvested = crop.harvest(tile.X, tile.Y, dirt, collector);
            int shippingBinCountAfter = farm.getShippingBin(Game1.MasterPlayer).Count;
            bool cropUnchanged = ReferenceEquals(dirt.crop, crop);

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
