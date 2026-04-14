using FarmingCapitalist.Workers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private WorkerBehaviorManager? workerBehaviorManager;
        private WorkerControlMenuController? workerControlMenuController;
        private WorkerCustomizationManager? workerCustomizationManager;
        private WorkerShellManager? workerShellManager;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.workerShellManager = new WorkerShellManager(helper, this.ModManifest, this.Monitor);
            WorkerNavigationManager workerNavigationManager = new(this.workerShellManager, this.Monitor);
            this.workerBehaviorManager = new WorkerBehaviorManager(workerNavigationManager, this.workerShellManager, this.Monitor);
            this.workerControlMenuController = new WorkerControlMenuController(helper.Input, this.workerShellManager);
            this.workerCustomizationManager = new WorkerCustomizationManager(this.Monitor, this.workerShellManager, this.workerBehaviorManager);

            helper.ConsoleCommands.Add(
                "workerstatus",
                "Logs the current location and tile for the test worker shell, plus whether an appearance is configured.",
                this.OnWorkerStatusCommand);
            helper.ConsoleCommands.Add(
                "spawn",
                "Use `spawn` to open the worker appearance menu, or `spawn d` to spawn/update the worker with the default appearance preset.",
                this.OnWorkerCustomizeSpawnCommand);
            helper.ConsoleCommands.Add(
                "delete",
                "Delete the test worker shell and clear its saved appearance so it won't respawn.",
                this.OnWorkerDeleteCommand);

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Input.ButtonPressed += this.workerControlMenuController.OnButtonPressed;
            helper.Events.Player.Warped += this.OnWarped;
        }


        /*********
        ** Private methods
        *********/
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.workerShellManager!.ReloadWorkerAppearance();
            this.workerShellManager.EnsureConfiguredWorkerPresent(respawnAtSpawn: true);
            this.workerBehaviorManager!.HandleConfiguredWorkersInitialized("save loaded");
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.workerShellManager!.EnsureConfiguredWorkerPresent(respawnAtSpawn: true);
            this.workerBehaviorManager!.HandleConfiguredWorkersInitialized("day started");
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            this.workerBehaviorManager!.Update();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.workerControlMenuController!.Reset();
            this.workerCustomizationManager!.Reset();
            this.workerBehaviorManager!.Reset();
            this.workerShellManager!.Reset();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || e.NewLocation.NameOrUniqueName != TestWorkerDefinition.LocationName)
            {
                return;
            }

            this.workerShellManager!.EnsureConfiguredWorkerPresent(respawnAtSpawn: false);
        }

        private void OnWorkerStatusCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first before checking worker status.", LogLevel.Info);
                return;
            }

            string configuredState = this.workerShellManager!.HasSavedWorkerAppearance()
                ? "configured"
                : "not configured";
            int configuredWorkerCount = this.workerShellManager.GetConfiguredWorkerCount();

            if (this.workerShellManager!.TryGetTestWorker(out NPC? worker))
            {
                string locationName = worker?.currentLocation?.NameOrUniqueName ?? "unknown";
                string tileText = worker?.Tile.ToString() ?? "unknown";

                this.Monitor.Log(
                    $"Primary worker found in {locationName} at tile {tileText}. Appearance is {configuredState}. Configured worker count: {configuredWorkerCount}. Expected base spawn tile: {this.workerShellManager.GetExpectedSpawnTile()}.",
                    LogLevel.Info);
                return;
            }

            this.Monitor.Log(
                $"No primary worker shell was found. Appearance is {configuredState}. Configured worker count: {configuredWorkerCount}. Expected location: {TestWorkerDefinition.LocationName}; expected base spawn tile: {this.workerShellManager.GetExpectedSpawnTile()}.",
                LogLevel.Warn);
        }

        private void OnWorkerCustomizeSpawnCommand(string command, string[] args)
        {
            if (args.Length > 0)
            {
                string mode = args[0].Trim().ToLowerInvariant();
                if (mode is "d" or "default")
                {
                    this.workerCustomizationManager!.SpawnWithDefaultAppearance();
                    return;
                }
            }

            this.workerCustomizationManager!.StartCustomizationSession();
        }

        private void OnWorkerDeleteCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first before deleting the worker shell.", LogLevel.Info);
                return;
            }

            this.workerBehaviorManager!.Reset();
            if (this.workerShellManager!.DeleteConfiguredWorker())
            {
                this.Monitor.Log("Deleted all spawned worker shells and cleared the saved worker roster.", LogLevel.Info);
                return;
            }

            this.Monitor.Log("No spawned worker shells or saved worker roster entries were found to delete.", LogLevel.Info);
        }
    }
}
