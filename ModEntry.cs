using FarmingCapitalist.Workers;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
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
            this.workerCustomizationManager = new WorkerCustomizationManager(this.Monitor, this.workerShellManager);

            Harmony harmony = new(this.ModManifest.UniqueID);
            WorkerNpcDrawPatch.Apply(harmony, this.workerShellManager);

            helper.ConsoleCommands.Add(
                "workerstatus",
                "Logs the current location and tile for the test worker shell, plus whether an appearance is configured.",
                this.OnWorkerStatusCommand);
            helper.ConsoleCommands.Add(
                "spawn",
                "Open the worker appearance menu, then save that appearance and spawn/update the worker shell.",
                this.OnWorkerCustomizeSpawnCommand);
            helper.ConsoleCommands.Add(
                "delete",
                "Delete the test worker shell and clear its saved appearance so it won't respawn.",
                this.OnWorkerDeleteCommand);

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Player.Warped += this.OnWarped;
        }


        /*********
        ** Private methods
        *********/
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.workerShellManager!.ReloadWorkerAppearance();
            this.workerShellManager.EnsureConfiguredWorkerPresent();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.workerShellManager!.EnsureConfiguredWorkerPresent();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            this.workerCustomizationManager!.Reset();
            this.workerShellManager!.Reset();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || e.NewLocation.NameOrUniqueName != TestWorkerDefinition.LocationName)
            {
                return;
            }

            this.workerShellManager!.EnsureConfiguredWorkerPresent();
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

            if (this.workerShellManager!.TryGetTestWorker(out NPC? worker))
            {
                string locationName = worker?.currentLocation?.NameOrUniqueName ?? "unknown";
                string tileText = worker?.Tile.ToString() ?? "unknown";

                this.Monitor.Log(
                    $"Test worker found in {locationName} at tile {tileText}. Appearance is {configuredState}. Expected spawn tile: {this.workerShellManager.GetExpectedSpawnTile()}.",
                    LogLevel.Info);
                return;
            }

            this.Monitor.Log(
                $"Test worker shell not found. Appearance is {configuredState}. Expected location: {TestWorkerDefinition.LocationName}; expected spawn tile: {this.workerShellManager.GetExpectedSpawnTile()}.",
                LogLevel.Warn);
        }

        private void OnWorkerCustomizeSpawnCommand(string command, string[] args)
        {
            this.workerCustomizationManager!.StartCustomizationSession();
        }

        private void OnWorkerDeleteCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first before deleting the worker shell.", LogLevel.Info);
                return;
            }

            if (this.workerShellManager!.DeleteConfiguredWorker())
            {
                this.Monitor.Log("Deleted the test worker shell and cleared its saved appearance.", LogLevel.Info);
                return;
            }

            this.Monitor.Log("No test worker shell or saved worker appearance was found to delete.", LogLevel.Info);
        }
    }
}
