using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerCustomizationManager
{
    private readonly IMonitor monitor;
    private readonly WorkerBehaviorManager workerBehaviorManager;
    private readonly WorkerShellManager workerShellManager;

    public WorkerCustomizationManager(IMonitor monitor, WorkerShellManager workerShellManager, WorkerBehaviorManager workerBehaviorManager)
    {
        this.monitor = monitor;
        this.workerShellManager = workerShellManager;
        this.workerBehaviorManager = workerBehaviorManager;
    }

    public void StartCustomizationSession()
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save first before opening the worker customizer.", LogLevel.Info);
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            this.monitor.Log("Close the current menu before opening the worker customizer.", LogLevel.Info);
            return;
        }

        WorkerAppearanceData appearance = this.workerShellManager.GetSavedWorkerAppearance()?.Clone() ?? WorkerAppearanceData.CreateDefault();
        Game1.activeClickableMenu = new WorkerAppearanceMenu(appearance, this.SaveCustomization);
        this.monitor.Log("Opened the worker appearance menu. Save there to update and spawn the worker shell.", LogLevel.Info);
    }

    public void SpawnWithDefaultAppearance()
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save first before spawning the worker.", LogLevel.Info);
            return;
        }

        this.SaveCustomization(WorkerAppearanceData.CreateDefault());
        this.monitor.Log("Spawned the test worker using the default appearance preset.", LogLevel.Info);
    }

    public void Reset()
    {
    }

    private void SaveCustomization(WorkerAppearanceData appearance)
    {
        NPC? worker = this.workerShellManager.SpawnConfiguredWorker(appearance);
        this.workerBehaviorManager.HandleWorkerInitialized(worker, "appearance update");

        this.monitor.Log(
            $"Saved the worker appearance and spawned a worker shell. Total configured workers: {this.workerShellManager.GetConfiguredWorkerCount()}.",
            LogLevel.Info);
    }
}
