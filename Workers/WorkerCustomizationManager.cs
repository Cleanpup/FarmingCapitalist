using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerCustomizationManager
{
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;

    public WorkerCustomizationManager(IMonitor monitor, WorkerShellManager workerShellManager)
    {
        this.monitor = monitor;
        this.workerShellManager = workerShellManager;
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

    public void Reset()
    {
    }

    private void SaveCustomization(WorkerAppearanceData appearance)
    {
        this.workerShellManager.SaveWorkerAppearance(appearance);
        this.workerShellManager.EnsureConfiguredWorkerPresent();
        this.monitor.Log(
            $"Saved the worker appearance and ensured the worker shell is present at {TestWorkerDefinition.LocationName} tile {this.workerShellManager.GetExpectedSpawnTile()}.",
            LogLevel.Info);
    }
}
