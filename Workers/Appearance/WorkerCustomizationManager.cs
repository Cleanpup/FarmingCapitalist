using System;
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
        this.StartHiringSession();
    }

    public void StartHiringSession(Action? onClosed = null)
    {
        if (!this.CanOpenHiring())
        {
            onClosed?.Invoke();
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            this.monitor.Log("Close the current menu before opening the worker customizer.", LogLevel.Info);
            onClosed?.Invoke();
            return;
        }

        WorkerAppearanceData appearance = this.workerShellManager.GetSavedWorkerAppearance() ?? WorkerAppearanceData.CreateDefault();
        Game1.activeClickableMenu = new WorkerAppearanceMenu(appearance, WorkerProfession.Farmer,
            this.workerShellManager.GetSuggestedWorkerName(), this.SaveCustomization, onClosed, "Hire Worker");
        this.monitor.Log($"Hiring costs {WorkerEmploymentTerms.HiringCost}g including today's wages, then {WorkerEmploymentTerms.DailyWage}g per day. Cancel to leave without hiring.", LogLevel.Info);
    }

    public void SpawnWithDefaultAppearance()
    {
        if (this.CanOpenHiring())
        {
            this.SaveCustomization(WorkerAppearanceData.CreateDefault(), WorkerProfession.Farmer,
                this.workerShellManager.GetSuggestedWorkerName());
        }
    }

    public void Reset()
    {
    }

    private bool CanOpenHiring()
    {
        if (!Context.IsWorldReady)
        {
            this.monitor.Log("Load a save before hiring workers.", LogLevel.Info);
            return false;
        }

        if (!Context.IsMainPlayer)
        {
            string message = "Only the farm host can hire workers.";
            this.monitor.Log(message, LogLevel.Info);
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
            return false;
        }

        return true;
    }

    private void SaveCustomization(WorkerAppearanceData appearance, WorkerProfession profession, string name)
    {
        bool hired = this.workerShellManager.TryHireWorker(appearance, profession, name, out NPC? worker, out string message);
        if (hired)
        {
            this.workerBehaviorManager.HandleWorkerInitialized(worker, "hired");
        }

        this.monitor.Log(message, hired ? LogLevel.Info : LogLevel.Warn);
        if (Context.IsWorldReady)
        {
            Game1.addHUDMessage(new HUDMessage(message, hired ? HUDMessage.newQuest_type : HUDMessage.error_type));
        }
    }
}
