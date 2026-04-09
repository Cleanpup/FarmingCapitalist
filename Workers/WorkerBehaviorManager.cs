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

    private readonly IMonitor monitor;
    private readonly WorkerNavigationManager navigationManager;
    private readonly WorkerShellManager workerShellManager;
    private readonly WorkerNavigationTarget initialTravelTarget = new(
        TestWorkerDefinition.InitialTravelLocationName,
        TestWorkerDefinition.InitialTravelTile,
        TestWorkerDefinition.InitialTravelFacingDirection);
    private readonly WorkerNavigationTarget returnTravelTarget = new(
        TestWorkerDefinition.LocationName,
        TestWorkerDefinition.SpawnTile,
        TestWorkerDefinition.FacingDirection);
    private WorkerTravelPhase activePhase;

    public WorkerBehaviorManager(WorkerNavigationManager navigationManager, WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.navigationManager = navigationManager;
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public void HandleWorkerInitialized(NPC? worker, string triggerReason)
    {
        if (worker is null)
        {
            return;
        }

        this.monitor.Log(
            $"Worker initialization trigger '{triggerReason}' is attempting travel to {this.initialTravelTarget.LocationName} tile {this.initialTravelTarget.Tile}.",
            LogLevel.Info);
        if (this.navigationManager.TryStartTravel(worker, this.initialTravelTarget, triggerReason))
        {
            this.activePhase = WorkerTravelPhase.TravellingToFarmTarget;
        }
        else
        {
            this.activePhase = WorkerTravelPhase.None;
        }
    }

    public void Update()
    {
        this.navigationManager.Update();

        if (!Context.IsWorldReady || !this.workerShellManager.TryGetTestWorker(out NPC? worker) || worker is null)
        {
            return;
        }

        switch (this.activePhase)
        {
            case WorkerTravelPhase.TravellingToFarmTarget:
                if (worker.currentLocation?.NameOrUniqueName == this.initialTravelTarget.LocationName
                    && worker.TilePoint == this.initialTravelTarget.Tile
                    && worker.controller is null)
                {
                    this.monitor.Log(
                        $"Worker reached the farm target and is now returning to {this.returnTravelTarget.LocationName} tile {this.returnTravelTarget.Tile}.",
                        LogLevel.Info);
                    if (this.navigationManager.TryStartTravel(worker, this.returnTravelTarget, "return to farmhouse"))
                    {
                        this.activePhase = WorkerTravelPhase.ReturningToFarmhouse;
                    }
                    else
                    {
                        this.activePhase = WorkerTravelPhase.None;
                    }
                }
                break;

            case WorkerTravelPhase.ReturningToFarmhouse:
                if (worker.currentLocation?.NameOrUniqueName == this.returnTravelTarget.LocationName
                    && worker.TilePoint == this.returnTravelTarget.Tile
                    && worker.controller is null)
                {
                    this.monitor.Log(
                        $"Worker completed the round trip and is back at {this.returnTravelTarget.LocationName} tile {this.returnTravelTarget.Tile}.",
                        LogLevel.Info);
                    this.activePhase = WorkerTravelPhase.None;
                }
                break;
        }
    }

    public void Reset()
    {
        this.activePhase = WorkerTravelPhase.None;
        this.navigationManager.Reset();
    }
}
