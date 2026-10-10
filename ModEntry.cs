using HireSkilledHelpers.Workers;
using HireSkilledHelpers.Integrations;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace HireSkilledHelpers;

/// <summary>Connects worker services to SMAPI; decisions live in the worker runtime.</summary>
internal sealed class ModEntry : Mod
{
    private WorkerBehaviorManager workerBehaviorManager = null!;
    private WorkerControlMenuController workerControlMenuController = null!;
    private WorkerCustomizationManager workerCustomizationManager = null!;
    private WorkerDialogueManager workerDialogueManager = null!;
    private WorkerShellManager workerShellManager = null!;
    private WorkerAccessManager workerAccess = null!;
    private WorkerPermitEventManager workerPermitEvent = null!;
    private ModConfig config = new();
    private bool workerFeaturesActive;

    public override void Entry(IModHelper helper)
    {
        this.config = helper.ReadConfig<ModConfig>();
        this.workerAccess = new WorkerAccessManager(helper, this.ModManifest, this.Monitor, this.config.BypassWorkerUnlockEvent);
        this.workerPermitEvent = new WorkerPermitEventManager(helper, this.Monitor,
            () => this.workerAccess.IsUnlocked, this.workerAccess.GrantPermission);
        this.workerShellManager = new WorkerShellManager(helper, this.ModManifest, this.Monitor,
            () => this.workerAccess.IsUnlocked);
        WorkerNavigationManager navigation = new(this.workerShellManager, this.Monitor);
        this.workerBehaviorManager = new WorkerBehaviorManager(navigation, this.workerShellManager, this.Monitor);
        this.workerDialogueManager = new WorkerDialogueManager(helper, this.workerShellManager, this.Monitor, this.ModManifest.UniqueID);
        Harmony harmony = new(this.ModManifest.UniqueID);
        this.workerBehaviorManager.RegisterCombatAnimations(harmony);
        this.workerDialogueManager.Register(harmony);
        WorkerExperienceSuppression.Register(harmony);
        this.workerCustomizationManager = new WorkerCustomizationManager(this.Monitor, this.workerShellManager, this.workerBehaviorManager);
        this.workerControlMenuController = new WorkerControlMenuController(helper.Input, this.workerShellManager, this.workerCustomizationManager, this.workerBehaviorManager);

        helper.ConsoleCommands.Add("workerpermit", "Queue Lewis's standalone farmhand-permission cutscene early. Host only; stand on the Farm and close menus to let the visit begin.", this.OnWorkerPermitCommand);
        helper.ConsoleCommands.Add("workers", "Manage hired workers. Use 'workers help' for commands, or 'workers' to open the menu.", this.OnWorkersCommand);
        helper.ConsoleCommands.Add("workerstatus", "List every worker's ID, assignment, activity and location.", (_, _) => this.LogWorkerStatus());
        helper.ConsoleCommands.Add("spawn", "Hire a worker with custom appearance (500g), or 'spawn d' for the default appearance.", this.OnWorkerHireCommand);
        helper.ConsoleCommands.Add("delete", "Use 'delete all' to dismiss all workers, or 'workers dismiss <id>' for one worker.", this.OnWorkerDeleteCommand);

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.Saving += this.OnSaving;
        helper.Events.GameLoop.Saved += this.OnSaved;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.Input.ButtonPressed += this.workerControlMenuController.OnButtonPressed;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (this.workerAccess.IsUnlocked)
        {
            this.workerBehaviorManager.DrawCombatHealthBars(e.SpriteBatch);
            this.workerBehaviorManager.DrawFishing(e.SpriteBatch);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            IGenericModConfigMenuApi? menu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (menu is null) return;
            menu.Register(this.ModManifest, reset: () => this.config = new ModConfig(), save: () =>
            {
                this.Helper.WriteConfig(this.config);
                this.workerAccess.SetBypass(this.config.BypassWorkerUnlockEvent);
            });
            menu.AddBoolOption(this.ModManifest, getValue: () => this.config.BypassWorkerUnlockEvent,
                setValue: value => this.config.BypassWorkerUnlockEvent = value,
                name: () => "Skip farmhand permission event",
                tooltip: () => "Enable Farm Crew immediately without waiting for Lewis's Spring 8 visit. The host's setting applies to the whole farm. Turning this off requires the visit unless this farm has already completed it.",
                fieldId: "BypassWorkerUnlockEvent");
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Optional Generic Mod Config Menu integration is unavailable: {ex.Message}", LogLevel.Warn);
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        WorkerSaveMigration.Migrate();
        this.workerPermitEvent.Reset();
        this.workerAccess.Load();
        this.workerShellManager.ReloadWorkerAppearance();
        this.workerFeaturesActive = this.workerAccess.IsUnlocked;
        if (!Context.IsMainPlayer) return;
        if (this.workerFeaturesActive) this.ActivateWorkers("save loaded", morning: false);
        else this.workerBehaviorManager.SuspendForAccessGate();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.workerAccess.Synchronize();
        this.workerFeaturesActive = this.workerAccess.IsUnlocked;
        if (!Context.IsMainPlayer)
        {
            this.workerShellManager.RefreshClientRoster();
            return;
        }
        this.workerBehaviorManager.Reset();
        if (this.workerFeaturesActive) this.ActivateWorkers("day started", morning: true);
        else this.workerBehaviorManager.SuspendForAccessGate();
    }

    private void ActivateWorkers(string reason, bool morning)
    {
        this.workerShellManager.RestoreWorkersAfterSaving();
        this.workerShellManager.ProcessDailyWages();
        this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: morning);
        if (morning) this.workerBehaviorManager.PlaceWorkersOnFarmForMorning();
        this.workerBehaviorManager.HandleConfiguredWorkersInitialized(reason);
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (!Context.IsMainPlayer)
            return;
        this.workerAccess.Save();
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
        if (!Context.IsWorldReady) return;
        this.workerAccess.Synchronize();
        this.workerPermitEvent.Update();
        bool unlocked = this.workerAccess.IsUnlocked;
        bool changed = unlocked != this.workerFeaturesActive;
        if (changed && Context.IsMainPlayer)
        {
            if (unlocked) this.ActivateWorkers("worker features unlocked", morning: false);
            else
            {
                this.workerControlMenuController.Reset();
                this.workerBehaviorManager.SuspendForAccessGate();
            }
        }
        this.workerFeaturesActive = unlocked;
        if (!Context.IsMainPlayer && (changed || e.IsMultipleOf(60)))
            this.workerShellManager.RefreshClientRoster();
        if (unlocked) this.workerBehaviorManager.Update();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.workerFeaturesActive = false;
        this.workerAccess.Reset();
        this.workerPermitEvent.Reset();
        this.workerControlMenuController.Reset();
        this.workerCustomizationManager.Reset();
        this.workerDialogueManager.Reset();
        this.workerBehaviorManager.Reset();
        this.workerShellManager.Reset();
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer || !Context.IsWorldReady || !this.workerAccess.IsUnlocked)
            return;
        if (!Context.IsMainPlayer)
            this.workerShellManager.RefreshClientRoster();
        else
        {
            if (e.NewLocation.NameOrUniqueName == TestWorkerDefinition.LocationName)
                this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: false);
            this.workerBehaviorManager.HandleHostWarp();
        }
    }

    private void OnWorkerPermitCommand(string command, string[] args)
    {
        if (args.Length != 0)
        {
            this.Monitor.Log("Use workerpermit with no arguments while on the Farm.", LogLevel.Info);
            return;
        }
        bool queued = this.workerPermitEvent.TryStartEarly(out string message);
        this.Monitor.Log(message, queued ? LogLevel.Info : LogLevel.Warn);
    }

    private void OnWorkersCommand(string command, string[] args)
    {
        string action = args.Length == 0 ? "menu" : args[0].ToLowerInvariant();
        if (action == "permit" && args.Length == 1)
        {
            this.OnWorkerPermitCommand(command, Array.Empty<string>());
            return;
        }
        if (action == "help")
        {
            this.Monitor.Log(
                $"Press B or use 'workers' to manage your crew. Hire: {WorkerEmploymentTerms.HiringCost}g including today's wage; later {WorkerEmploymentTerms.DailyWage}g/day.\n"
                + "workerpermit — queue Lewis's standalone permission cutscene early; host on the Farm\n"
                + "workers status — list IDs, orders, activity and location\n"
                + "workers hire [default] — hire with custom or default appearance\n"
                + "workers assign <id> <water|harvest|tend|forage|trees|hardwood|debris|ores|ladder|mine|slay|explore|fish|idle> — assign a job\n"
                + "workers combat-area <id> <farm|mines|skull|islandfarm|volcanoentrance> — set a Fighter's area\n"
                + "workers fishing-area <id> <forest|mountain|town|beach|island> — set simulated fishing shore\n"
                + "workers mining-area <id> <quarry|mines|skull|volcano> — set physical mining area\n"
                + "workers explore-area <id> <mines|skull|volcano> — set simulated exploration area\n"
                + "workers dismiss <id> — dismiss one worker\n"
                + "workers pay — retry unpaid wages without charging paid workers again\n"
                + "Choose each worker's harvest destination in the Storage tab; the shipping bin is the default and overflow fallback. Only the host can manage workers.", LogLevel.Info);
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
                    "forage" or "collectforage" => WorkerTaskKind.CollectForage,
                    "trees" or "choptrees" => WorkerTaskKind.ChopTrees,
                    "hardwood" or "chophardwood" => WorkerTaskKind.ChopHardwood,
                    "debris" or "cleardebris" => WorkerTaskKind.ClearDebris,
                    "mine" or "mining" or "minerocks" or "allstone" => WorkerTaskKind.MineRocks,
                    "slay" or "combat" or "slaymonsters" => WorkerTaskKind.SlayMonsters,
                    "ores" or "gems" or "ore" => WorkerTaskKind.MineOreGems,
                    "ladder" or "findladder" => WorkerTaskKind.FindLadder,
                    "explore" or "explorearea" => WorkerTaskKind.ExploreArea,
                    "fish" or "fishing" => WorkerTaskKind.Fish,
                    "idle" or "stop" => WorkerTaskKind.Idle,
                    _ => null,
                };
                if (task is null)
                {
                    this.Monitor.Log("Choose water, harvest, tend, forage, trees, hardwood, debris, ores, ladder, mine, slay, explore, fish, or idle. Use 'workers status' to find worker IDs.", LogLevel.Info);
                    return;
                }
                bool assigned = this.workerBehaviorManager.TryAssignTask(args[1], task.Value, out string assignmentMessage);
                this.Monitor.Log(assignmentMessage, assigned ? LogLevel.Info : LogLevel.Warn);
                break;
            case "fishing-area" when args.Length == 3:
                if (!this.RequireHost()) return;
                string fishingArea = args[2].ToLowerInvariant() switch
                {
                    "forest" => WorkerFishingAreaCatalog.Forest, "mountain" => WorkerFishingAreaCatalog.Mountain,
                    "town" => WorkerFishingAreaCatalog.Town, "beach" => WorkerFishingAreaCatalog.Beach,
                    "island" => WorkerFishingAreaCatalog.Island, _ => args[2],
                };
                bool fishingChanged = this.workerBehaviorManager.TrySetFishingArea(args[1], fishingArea, out string fishingMessage);
                this.Monitor.Log(fishingMessage, fishingChanged ? LogLevel.Info : LogLevel.Warn);
                break;
            case "mining-area" when args.Length == 3:
                if (!this.RequireHost()) return;
                string? miningArea = args[2].ToLowerInvariant() switch
                {
                    "farm" => WorkerMiningPolicy.Farm,
                    "quarry" => WorkerMiningPolicy.Quarry,
                    "mines" or "mine" => WorkerMiningPolicy.Mines,
                    "skull" or "skullcavern" => WorkerMiningPolicy.SkullCavern,
                    "volcano" => WorkerMiningPolicy.Volcano,
                    _ => null,
                };
                if (miningArea is null)
                {
                    this.Monitor.Log("Choose quarry, mines, skull, or volcano.", LogLevel.Info);
                    return;
                }
                bool miningChanged = this.workerBehaviorManager.TrySetMiningArea(args[1], miningArea, out string miningMessage);
                this.Monitor.Log(miningMessage, miningChanged ? LogLevel.Info : LogLevel.Warn);
                break;
            case "combat-area" when args.Length == 3:
                if (!this.RequireHost())
                    return;
                string? area = args[2].ToLowerInvariant() switch
                {
                    "farm" => WorkerCombatAreaCatalog.Farm,
                    "mines" or "mine" => WorkerCombatAreaCatalog.Mines,
                    "skull" or "skullcavern" => WorkerCombatAreaCatalog.SkullCavern,
                    "islandfarm" => WorkerCombatAreaCatalog.IslandFarm,
                    "volcanoentrance" => WorkerCombatAreaCatalog.VolcanoEntrance,
                    _ => null,
                };
                if (area is null)
                {
                    this.Monitor.Log("Choose farm, mines, skull, islandfarm, or volcanoentrance.", LogLevel.Info);
                    return;
                }
                bool changed = this.workerBehaviorManager.TrySetCombatArea(args[1], area, out string areaMessage);
                this.Monitor.Log(areaMessage, changed ? LogLevel.Info : LogLevel.Warn);
                break;
            case "explore-area" when args.Length == 3:
                if (!this.RequireHost()) return;
                string? explorationArea = args[2].ToLowerInvariant() switch
                {
                    "mines" or "mine" => WorkerExplorationAreaCatalog.Mines,
                    "skull" or "skullcave" or "skullcavern" => WorkerExplorationAreaCatalog.SkullCavern,
                    "volcano" => WorkerExplorationAreaCatalog.Volcano,
                    _ => null,
                };
                if (explorationArea is null)
                {
                    this.Monitor.Log("Choose mines, skull, or volcano.", LogLevel.Info);
                    return;
                }
                bool explorationChanged = this.workerBehaviorManager.TrySetExplorationArea(args[1], explorationArea, out string explorationMessage);
                this.Monitor.Log(explorationMessage, explorationChanged ? LogLevel.Info : LogLevel.Warn);
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
                this.workerShellManager.ProcessDailyWages(retryUnpaid: true);
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
            var stamina = this.workerShellManager.GetWorkerStamina(worker.WorkerId);
            this.Monitor.Log($"{worker.DisplayName} [{worker.WorkerId}] — {WorkerTaskPolicy.GetProfessionLabel(worker.Profession)}, {this.workerShellManager.GetAssignedTask(worker.WorkerId)}; {activity}; stamina: {stamina.Current:0.#}/{stamina.Maximum:0}; location: {worker.CurrentLocationName ?? "not spawned"}, tile: {worker.CurrentTile?.ToString() ?? "unknown"}; paid today: {this.workerShellManager.CanWorkerWorkToday(worker.WorkerId)}.", LogLevel.Info);
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
        {
            if (this.workerAccess.IsUnlocked) return true;
            this.Monitor.Log(WorkerUnlockPolicy.LockedMessage, LogLevel.Info);
            return false;
        }
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
