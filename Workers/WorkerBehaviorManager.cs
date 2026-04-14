using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

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
    private readonly WorkerNavigationTarget initialTravelTarget = new(
        TestWorkerDefinition.InitialTravelLocationName,
        TestWorkerDefinition.InitialTravelTile,
        TestWorkerDefinition.InitialTravelFacingDirection);

    public WorkerBehaviorManager(WorkerNavigationManager navigationManager, WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.navigationManager = navigationManager;
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public void HandleWorkerInitialized(NPC? worker, string triggerReason)
    {
        if (worker is null || !this.workerShellManager.TryGetWorkerId(worker, out string workerId))
        {
            return;
        }

        this.monitor.Log(
            $"{worker.displayName} initialization trigger '{triggerReason}' is attempting travel to {this.initialTravelTarget.LocationName} tile {this.initialTravelTarget.Tile}.",
            LogLevel.Info);
        if (this.navigationManager.TryStartTravel(worker, this.initialTravelTarget, triggerReason))
        {
            this.activePhases[workerId] = WorkerTravelPhase.TravellingToFarmTarget;
        }
        else
        {
            this.activePhases[workerId] = WorkerTravelPhase.None;
        }
    }

    public void HandleConfiguredWorkersInitialized(string triggerReason)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        foreach (NPC worker in this.workerShellManager.GetSpawnedWorkers())
        {
            this.HandleWorkerInitialized(worker, triggerReason);
        }
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

            WorkerTravelPhase phase = this.activePhases.GetValueOrDefault(workerId, WorkerTravelPhase.None);
            switch (phase)
            {
                case WorkerTravelPhase.TravellingToFarmTarget:
                    if (worker.currentLocation?.NameOrUniqueName == this.initialTravelTarget.LocationName
                        && worker.TilePoint == this.initialTravelTarget.Tile
                        && worker.controller is null)
                    {
                        if (!this.workerShellManager.TryGetWorkerReturnTarget(worker, out WorkerNavigationTarget returnTravelTarget))
                        {
                            this.activePhases[workerId] = WorkerTravelPhase.None;
                            continue;
                        }

                        this.monitor.Log(
                            $"{worker.displayName} reached the farm target and is now returning to {returnTravelTarget.LocationName} tile {returnTravelTarget.Tile}.",
                            LogLevel.Info);
                        if (this.navigationManager.TryStartTravel(worker, returnTravelTarget, "return to farmhouse"))
                        {
                            this.activePhases[workerId] = WorkerTravelPhase.ReturningToFarmhouse;
                        }
                        else
                        {
                            this.activePhases[workerId] = WorkerTravelPhase.None;
                        }
                    }
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
        this.navigationManager.Reset();
    }
}
