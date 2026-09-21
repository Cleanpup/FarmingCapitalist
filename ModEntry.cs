using FarmingCapitalist.Workers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist;

/// <summary>Connects worker services to SMAPI; decisions live in the worker runtime.</summary>
internal sealed class ModEntry : Mod
{
    private WorkerBehaviorManager workerBehaviorManager = null!;
    private WorkerControlMenuController workerControlMenuController = null!;
    private WorkerCustomizationManager workerCustomizationManager = null!;
    private WorkerShellManager workerShellManager = null!;

    public override void Entry(IModHelper helper)
    {
        this.workerShellManager = new WorkerShellManager(helper, this.ModManifest, this.Monitor);
        WorkerNavigationManager navigation = new(this.workerShellManager, this.Monitor);
        this.workerBehaviorManager = new WorkerBehaviorManager(navigation, this.workerShellManager, this.Monitor);
        this.workerCustomizationManager = new WorkerCustomizationManager(this.Monitor, this.workerShellManager, this.workerBehaviorManager);
        this.workerControlMenuController = new WorkerControlMenuController(helper.Input, this.workerShellManager, this.workerCustomizationManager, this.workerBehaviorManager);

        helper.ConsoleCommands.Add("workers", "Manage hired workers. Use 'workers help' for commands, or 'workers' to open the menu.", this.OnWorkersCommand);
        helper.ConsoleCommands.Add("workerstatus", "List every worker's ID, assignment, activity and location.", (_, _) => this.LogWorkerStatus());
        helper.ConsoleCommands.Add("spawn", "Hire a worker with custom appearance (500g), or 'spawn d' for the default appearance.", this.OnWorkerHireCommand);
        helper.ConsoleCommands.Add("delete", "Use 'delete all' to dismiss all workers, or 'workers dismiss <id>' for one worker.", this.OnWorkerDeleteCommand);

        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.Saved += this.OnSaved;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.Input.ButtonPressed += this.workerControlMenuController.OnButtonPressed;
        helper.Events.Player.Warped += this.OnWarped;
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.workerShellManager.ReloadWorkerAppearance();
        if (!Context.IsMainPlayer)
            return;
        this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: true);
        this.workerBehaviorManager.HandleConfiguredWorkersInitialized("save loaded");
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!Context.IsMainPlayer)
        {
            this.workerShellManager.RefreshClientRoster();
            return;
        }
        this.workerBehaviorManager.Reset();
        this.workerShellManager.ProcessDailyWages();
        this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: true);
        this.workerBehaviorManager.HandleConfiguredWorkersInitialized("day started");
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        this.workerShellManager.SaveRoster();
        this.workerShellManager.RemoveWorkersForSaving();
    }

    private void OnSaved(object? sender, SavedEventArgs e)
    {
        if (Context.IsMainPlayer)
            this.workerShellManager.RestoreWorkersAfterSaving();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
        if (!Context.IsMainPlayer && e.IsMultipleOf(60))
            this.workerShellManager.RefreshClientRoster();
        this.workerBehaviorManager.Update();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.workerControlMenuController.Reset();
        this.workerCustomizationManager.Reset();
        this.workerBehaviorManager.Reset();
        this.workerShellManager.Reset();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer || !Context.IsWorldReady)
            return;
        if (!Context.IsMainPlayer)
            this.workerShellManager.RefreshClientRoster();
        else if (e.NewLocation.NameOrUniqueName == TestWorkerDefinition.LocationName)
            this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: false);
    }

    private void OnWorkersCommand(string command, string[] args)
    {
        string action = args.Length == 0 ? "menu" : args[0].ToLowerInvariant();
        if (action == "help")
        {
            this.Monitor.Log(
                $"Press B or use 'workers' to manage your crew. Hire: {WorkerEmploymentTerms.HiringCost}g including today's wage; later {WorkerEmploymentTerms.DailyWage}g/day.\n"
                + "workers status — list IDs, orders, activity and location\n"
                + "workers hire [default] — hire with custom or default appearance\n"
                + "workers assign <id> <water|harvest|tend|idle> — assign farm work or return home\n"
                + "workers dismiss <id> — dismiss one worker\n"
                + "workers pay — retry unpaid wages without charging paid workers again\n"
                + "Harvested crops go to the farm shipping bin. Only the host can manage workers.", LogLevel.Info);
            return;
        }
        if (!this.RequireWorld())
            return;
        switch (action)
        {
            case "menu":
                this.workerControlMenuController.OpenMenu();
                break;
            case "status":
            case "list":
                this.LogWorkerStatus();
                break;
            case "hire":
                this.OnWorkerHireCommand(command, args.Skip(1).ToArray());
                break;
            case "assign" when args.Length == 3:
                if (!this.RequireHost())
                    return;
                WorkerTaskKind? task = args[2].ToLowerInvariant() switch
                {
                    "water" or "watercrops" => WorkerTaskKind.WaterCrops,
                    "harvest" or "harvestcrops" => WorkerTaskKind.HarvestCrops,
                    "tend" or "tendcrops" => WorkerTaskKind.TendCrops,
                    "idle" or "stop" => WorkerTaskKind.Idle,
                    _ => null,
                };
                if (task is null)
                {
                    this.Monitor.Log("Choose water, harvest, tend, or idle. Use 'workers status' to find worker IDs.", LogLevel.Info);
                    return;
                }
                bool assigned = this.workerBehaviorManager.TryAssignTask(args[1], task.Value, out string assignmentMessage);
                this.Monitor.Log(assignmentMessage, assigned ? LogLevel.Info : LogLevel.Warn);
                break;
            case "dismiss" when args.Length == 2:
                if (!this.RequireHost())
                    return;
                this.workerBehaviorManager.StopWorker(args[1]);
                bool dismissed = this.workerShellManager.TryDismissWorker(args[1], out string dismissalMessage);
                this.Monitor.Log(dismissalMessage, dismissed ? LogLevel.Info : LogLevel.Warn);
                break;
            case "pay":
                if (!this.RequireHost())
                    return;
                this.workerShellManager.ProcessDailyWages();
                this.LogWorkerStatus();
                break;
            default:
                this.Monitor.Log("Unknown or incomplete command. Use 'workers help' for usage.", LogLevel.Info);
                break;
        }
    }

    private void LogWorkerStatus()
    {
        if (!this.RequireWorld())
            return;
        IReadOnlyList<WorkerSummarySnapshot> workers = this.workerShellManager.GetWorkerSummaries();
        if (workers.Count == 0)
            this.Monitor.Log("No workers hired. Press B or use 'workers hire' to hire one.", LogLevel.Info);
        foreach (WorkerSummarySnapshot worker in workers)
        {
            var activity = this.workerBehaviorManager.GetRuntimeSnapshot(worker.WorkerId);
            this.Monitor.Log($"{worker.DisplayName} [{worker.WorkerId}] — {this.workerShellManager.GetAssignedTask(worker.WorkerId)}; {activity}; location: {worker.CurrentLocationName ?? "not spawned"}, tile: {worker.CurrentTile?.ToString() ?? "unknown"}; paid today: {this.workerShellManager.CanWorkerWorkToday(worker.WorkerId)}.", LogLevel.Info);
        }
    }

    private void OnWorkerHireCommand(string command, string[] args)
    {
        if (!this.RequireWorld() || !this.RequireHost())
            return;
        if (args.Length > 0 && args[0].ToLowerInvariant() is "d" or "default")
            this.workerCustomizationManager.SpawnWithDefaultAppearance();
        else
            this.workerCustomizationManager.StartHiringSession();
    }

    private void OnWorkerDeleteCommand(string command, string[] args)
    {
        if (!this.RequireWorld() || !this.RequireHost())
            return;
        if (args.Length != 1 || !string.Equals(args[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            this.Monitor.Log("Use 'workers dismiss <id>' for one worker, or 'delete all' to dismiss the entire roster.", LogLevel.Info);
            return;
        }
        this.workerBehaviorManager.Reset();
        bool deleted = this.workerShellManager.DeleteConfiguredWorker();
        this.Monitor.Log(deleted ? "Dismissed every worker and cleared the saved roster." : "No workers to dismiss.", LogLevel.Info);
    }

    private bool RequireWorld()
    {
        if (Context.IsWorldReady)
            return true;
        this.Monitor.Log("Load a save before managing workers.", LogLevel.Info);
        return false;
    }

    private bool RequireHost()
    {
        if (Context.IsMainPlayer)
            return true;
        this.Monitor.Log("Only the host can hire, assign, pay or dismiss workers. Press B to view the crew.", LogLevel.Info);
        return false;
    }
}
