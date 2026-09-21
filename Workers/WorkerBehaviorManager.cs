using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerBehaviorManager
{
    private enum WorkerTravelPhase
    {
        None,
        TravellingToFarmTarget,
        ReturningToFarmhouse,
    }

    private readonly Dictionary<string, WorkerTravelPhase> activePhases = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly IMonitor monitor;
    private readonly WorkerNavigationManager navigationManager;
    private readonly WorkerShellManager workerShellManager;
    private readonly Dictionary<string, WorkerRuntimeSnapshot> snapshots = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> activeTargets = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> lastDebugStates = new(System.StringComparer.OrdinalIgnoreCase);

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

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
        {
            if (!this.workerShellManager.TryGetWorkerId(worker, out string workerId))
            {
                continue;
            }

            WorkerTaskKind assignment = this.workerShellManager.GetAssignedTask(workerId);
            if (assignment != WorkerTaskKind.Idle && this.workerShellManager.CanWorkerWorkToday(workerId))
            {
                this.UpdateAssignedWork(worker, workerId, assignment);
                continue;
            }

            WorkerTravelPhase phase = this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None);
            switch (phase)
            {
                case WorkerTravelPhase.TravellingToFarmTarget:
                    break;

                case WorkerTravelPhase.ReturningToFarmhouse:
                    if (!this.workerShellManager.TryGetWorkerReturnTarget(worker, out WorkerNavigationTarget homeTarget))
                    {
                        this.activePhases[workerId] = WorkerTravelPhase.None;
                        continue;
                    }

                    if (worker.currentLocation?.NameOrUniqueName == homeTarget.LocationName
                        && worker.TilePoint == homeTarget.Tile
                        && worker.controller is null)
                    {
                        this.monitor.Log(
                            $"{worker.displayName} completed the round trip and is back at {homeTarget.LocationName} tile {homeTarget.Tile}.",
                            LogLevel.Info);
                        this.activePhases[workerId] = WorkerTravelPhase.None;
                    }
                    break;
            }
        }
    }

    public void Reset()
    {
        this.activePhases.Clear();
        this.snapshots.Clear();
        this.activeTargets.Clear();
        this.lastDebugStates.Clear();
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
        if (!this.workerShellManager.TrySetAssignedTask(workerId, task))
        {
            message = "Only the host can assign worker tasks.";
            return false;
        }
        this.navigationManager.StopWorker(worker);
        this.activePhases[workerId] = WorkerTravelPhase.None;
        this.activeTargets.Remove(workerId);
        if (task == WorkerTaskKind.Idle && this.workerShellManager.TryGetWorkerReturnTarget(worker, out WorkerNavigationTarget homeTarget))
        {
            this.monitor.Log($"{worker.displayName} was set idle; returning home to {homeTarget.LocationName} tile {homeTarget.Tile}.", LogLevel.Info);
            if (this.navigationManager.TryStartTravel(worker, homeTarget, "idle/return home"))
                this.activePhases[workerId] = WorkerTravelPhase.ReturningToFarmhouse;
            else
                this.monitor.Log($"{worker.displayName} could not start its return-home route from {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}.", LogLevel.Warn);
        }
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(task, task == WorkerTaskKind.Idle ? "Idle" : "Selecting", $"Assigned: {WorkerTaskPolicy.GetTaskLabel(task)}", 0, null);
        this.LogState(workerId, worker, $"assignment changed to {task}");
        message = $"{worker.displayName} is now assigned to {WorkerTaskPolicy.GetTaskLabel(task)}.";
        return true;
    }

    public void StopWorker(string workerId)
    {
        if (this.workerShellManager.TryGetWorker(workerId, out NPC? worker) && worker is not null)
            this.navigationManager.StopWorker(worker);
        this.activeTargets.Remove(workerId);
        this.activePhases.Remove(workerId);
    }

    public WorkerRuntimeSnapshot GetRuntimeSnapshot(string workerId)
    {
        return this.snapshots.TryGetValue(workerId, out WorkerRuntimeSnapshot snapshot)
            ? snapshot
            : new WorkerRuntimeSnapshot(this.workerShellManager.GetAssignedTask(workerId), "Idle", "Waiting for an assignment", 0, null);
    }

    private void UpdateAssignedWork(NPC worker, string workerId, WorkerTaskKind assignment)
    {
        if (Game1.timeOfDay < 600 || Game1.timeOfDay >= 2200 || worker.controller is not null)
            return;
        Farm farm = Game1.getFarm();
        if (worker.currentLocation != farm)
        {
            this.LogState(workerId, worker, $"not at farm ({worker.currentLocation?.NameOrUniqueName ?? "unknown"}); routing to farm staging tile {new Point(60, 20)}");
            this.StartJobTravel(worker, workerId, new Point(60, 20), assignment, "travel to farm");
            return;
        }

        if (this.activeTargets.TryGetValue(workerId, out Point target) && worker.TilePoint == target && worker.controller is null)
        {
            this.PerformJobAt(farm, target, assignment);
            this.activeTargets.Remove(workerId);
            this.LogState(workerId, worker, $"finished action at target tile {target}; rescanning farm");
        }

        if (this.activeTargets.ContainsKey(workerId))
            return;

        foreach (KeyValuePair<Vector2, TerrainFeature> pair in farm.terrainFeatures.Pairs.OrderBy(p => p.Key.X + p.Key.Y))
        {
            if (pair.Value is not HoeDirt dirt || dirt.crop is null || dirt.crop.dead.Value)
                continue;
            bool harvestable = IsHarvestable(dirt.crop);
            bool needsWater = !dirt.isWatered();
            WorkerTaskKind action = WorkerTaskPolicy.SelectCropAction(assignment, true, harvestable, needsWater);
            if (action == WorkerTaskKind.Idle)
                continue;
            this.StartJobTravel(worker, workerId, pair.Key.ToPoint(), action, "travel to crop");
            return;
        }
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(assignment, "Idle", "No matching crops available", 0, null);
        this.LogState(workerId, worker, $"no matching crop found for {assignment}; worker is idle at {worker.currentLocation?.NameOrUniqueName ?? "unknown"} tile {worker.TilePoint}");
    }

    private void StartJobTravel(NPC worker, string workerId, Point target, WorkerTaskKind task, string reason)
    {
        this.activeTargets[workerId] = target;
        this.snapshots[workerId] = new WorkerRuntimeSnapshot(task, "Traveling", $"Walking to crop at {target}", 0, target);
        this.LogState(workerId, worker, $"selected {task} target tile {target}; starting route ({reason})");
        if (!this.navigationManager.TryStartTravel(worker, new WorkerNavigationTarget("Farm", target, 2), reason))
        {
            this.LogState(workerId, worker, $"route start failed for target tile {target}; clearing target and waiting for next update");
            this.activeTargets.Remove(workerId);
        }
    }

    private void PerformJobAt(Farm farm, Point tile, WorkerTaskKind task)
    {
        if (!farm.terrainFeatures.TryGetValue(tile.ToVector2(), out TerrainFeature? feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            this.monitor.Log($"Worker action skipped at tile {tile}: no HoeDirt/crop exists when the worker arrived.", LogLevel.Trace);
            return;
        }
        this.monitor.Log($"Worker action check at tile {tile}: assignment={task}, watered={dirt.isWatered()}, harvestable={IsHarvestable(dirt.crop)}, crop={dirt.crop.indexOfHarvest.Value ?? "none"}.", LogLevel.Trace);
        if (task == WorkerTaskKind.WaterCrops && !dirt.isWatered())
        {
            dirt.state.Value = HoeDirt.watered;
            this.monitor.Log($"Worker watered crop tile {tile}.", LogLevel.Trace);
            return;
        }
        if (task == WorkerTaskKind.HarvestCrops && IsHarvestable(dirt.crop))
        {
            Crop crop = dirt.crop;
            this.monitor.Log($"Worker reached crop tile {tile}; harvesting crop {crop.indexOfHarvest.Value} (regrows={crop.RegrowsAfterHarvest()}).", LogLevel.Trace);
            ShippingBinHarvester collector = new(farm, tile);
            bool harvested = crop.harvest(tile.X, tile.Y, dirt, collector);
            this.monitor.Log($"Worker harvest at tile {tile} completed={harvested}; crop remains={dirt.crop is not null}; shipping bin count={farm.getShippingBin(Game1.MasterPlayer).Count}.", LogLevel.Trace);
        }
    }

    private void LogState(string workerId, NPC worker, string state)
    {
        if (this.lastDebugStates.TryGetValue(workerId, out string? previous) && string.Equals(previous, state, StringComparison.Ordinal))
            return;
        this.lastDebugStates[workerId] = state;
        this.monitor.Log($"{worker.displayName} [{workerId}] runtime: {state}; assignment={this.workerShellManager.GetAssignedTask(workerId)}; location={worker.currentLocation?.NameOrUniqueName ?? "unknown"}; tile={worker.TilePoint}; controller={worker.controller?.GetType().Name ?? "none"}.", LogLevel.Trace);
    }

    private static bool IsHarvestable(Crop crop)
    {
        // A regrowing crop stays fully grown after harvest. Its day-of-current-phase
        // must be zero before it is available again.
        return crop.fullyGrown.Value
            && crop.indexOfHarvest is not null
            && (!crop.RegrowsAfterHarvest() || crop.dayOfCurrentPhase.Value <= 0);
    }

    private sealed class ShippingBinHarvester : JunimoHarvester
    {
        private readonly Farm farm;
        private readonly Point tile;

        public ShippingBinHarvester(Farm farm, Point tile)
        {
            this.farm = farm;
            this.tile = tile;
            this.currentLocation = farm;
            this.Position = tile.ToVector2() * Game1.tileSize;
        }

        public override void tryToAddItemToHut(Item item)
        {
            this.farm.getShippingBin(Game1.MasterPlayer).Add(item);
            this.farm.playSound("harvest");
        }
    }
}
