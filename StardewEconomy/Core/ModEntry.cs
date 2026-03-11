using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private ShopEditor _shopEditor = null!;
        private CropEconomyCsvExporter _cropEconomyCsvExporter = null!;

        public override void Entry(IModHelper helper)
        {
            ModConfig config = helper.ReadConfig<ModConfig>();
            VerbosePriceTraceLogger.Initialize(this.Monitor, config.EnableVerbosePriceTrace);
            SaveEconomyProfileService.Initialize(helper, this.Monitor);

            _shopEditor = new ShopEditor(helper, this.Monitor);
            _cropEconomyCsvExporter = new CropEconomyCsvExporter(helper, this.Monitor);

            helper.ConsoleCommands.Add(
                "starecon_dump",
                "Export crop economy debug CSV with modified seed/sell prices and 50-tile profit.",
                this.OnStareconDumpCommand
            );
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

        }
        
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // initialize Harmony patches for Economy and ShopMenu
            var harmonyId = this.ModManifest.UniqueID + ".economy";
            EconomyPatches.Initialize(this.Monitor, harmonyId);

            this.Monitor.Log("Game launched with Farming Capitalist!", LogLevel.Info);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            SaveEconomyProfileService.LoadOrCreateForCurrentSave();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            SaveEconomyProfileService.ClearActiveProfile();
        }

        private void OnStareconDumpCommand(string command, string[] args)
        {
            _ = command;
            _ = args;

            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save before running starecon_dump.", LogLevel.Warn);
                return;
            }

            if (_cropEconomyCsvExporter.TryExport(out string outputPath))
            {
                this.Monitor.Log($"starecon_dump export complete: {outputPath}", LogLevel.Info);
                return;
            }

            this.Monitor.Log("starecon_dump export failed. Check prior log errors for details.", LogLevel.Error);
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: this.Monitor);
            EconomyPatches.FrozenOvernightSellContext = context;

            this.Monitor.Log(
                $"Captured frozen overnight sell context for {context.Season} {context.DayOfMonth}.",
                LogLevel.Trace
            );
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (EconomyPatches.FrozenOvernightSellContext != null)
            {
                this.Monitor.Log("Clearing frozen overnight sell context on DayStarted.", LogLevel.Trace);
            }

            EconomyPatches.FrozenOvernightSellContext = null;
            DailyPurchaseTracker.ResetForNewDay();
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is not StardewValley.Menus.ShopMenu shop)
                return;

            DeferOneTick(() => _shopEditor.Apply(shop));
        }

        private void DeferOneTick(Action action)
        {
            void ApplyNextTick(object? s, StardewModdingAPI.Events.UpdateTickedEventArgs args)
            {
                this.Helper.Events.GameLoop.UpdateTicked -= ApplyNextTick;

                try
                {
                    action(); // many things going on here // shopeditor.apply being run
                }
                catch (Exception e)
                {
                    this.Monitor.Log($"Failed to apply shop changes: {e}", LogLevel.Error);
                }
            }

            this.Helper.Events.GameLoop.UpdateTicked += ApplyNextTick;
        }

        private string GetItemName(StardewValley.ISalable s)
        {
            if (s is StardewValley.Object obj) return obj.Name;
            if (s is StardewValley.Item it) return it.Name;
            return s?.ToString() ?? "<unknown>";
        }

    }
}
