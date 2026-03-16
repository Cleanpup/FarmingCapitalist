using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        internal static ModConfig Config { get; private set; } = new();

        private ShopEditor _shopEditor = null!;
        private CropEconomyCsvExporter _cropEconomyCsvExporter = null!;
        private SupplyDebugCommandService _supplyDebugCommands = null!;
        private IMarketSimulationLifecycle _cropMarketSimulation = null!;
        private IMarketSimulationLifecycle _fishMarketSimulation = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            ItemCategoryRules.Initialize(Config.FishClassification);
            VerbosePriceTraceLogger.Initialize(this.Monitor, Config.EnableVerbosePriceTrace);
            SaveEconomyProfileService.Initialize(helper, this.Monitor);
            CropSupplyDataService.Initialize(helper, this.Monitor);
            FishSupplyDataService.Initialize(helper, this.Monitor);
            CropSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            FishSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);

            _shopEditor = new ShopEditor(helper, this.Monitor);
            _cropEconomyCsvExporter = new CropEconomyCsvExporter(helper, this.Monitor);
            _supplyDebugCommands = new SupplyDebugCommandService(this.Monitor);
            _cropMarketSimulation = new CropMarketSimulationLifecycleAdapter();
            _fishMarketSimulation = new FishMarketSimulationLifecycleAdapter();
            _cropMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _fishMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);

            helper.ConsoleCommands.Add(
                "starecon_dump",
                "Export crop economy debug CSV with modified seed/sell prices and 50-tile profit.",
                this.OnStareconDumpCommand
            );
            _supplyDebugCommands.Register(helper);
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
            _ = sender;
            _ = e;

            if (!Context.IsWorldReady)
                return;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            _ = sender;
            _ = e;

            string harmonyId = this.ModManifest.UniqueID + ".economy";
            EconomyPatches.Initialize(this.Monitor, harmonyId);

            this.Monitor.Log("Stardew Economy Successful, be sure to post feedback on Nexus!", LogLevel.Trace);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _ = sender;
            _ = e;

            SaveEconomyProfileService.LoadOrCreateForCurrentSave();
            CropSupplyDataService.LoadOrCreateForCurrentSave();
            _cropMarketSimulation.LoadOrCreateForCurrentSave();
            _fishMarketSimulation.LoadOrCreateForCurrentSave();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            _ = sender;
            _ = e;

            SaveEconomyProfileService.ClearActiveProfile();
            CropSupplyDataService.ClearActiveData();
            _cropMarketSimulation.ClearActiveData();
            _fishMarketSimulation.ClearActiveData();
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
            _ = sender;
            _ = e;

            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: this.Monitor);
            EconomyPatches.FrozenOvernightSellContext = context;

            this.Monitor.Log(
                $"Captured frozen overnight sell context for {context.Season} {context.DayOfMonth}.",
                LogLevel.Trace
            );
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            _ = sender;
            _ = e;

            if (EconomyPatches.FrozenOvernightSellContext != null)
                this.Monitor.Log("Clearing frozen overnight sell context on DayStarted.", LogLevel.Trace);

            EconomyPatches.FrozenOvernightSellContext = null;
            DailyPurchaseTracker.ResetForNewDay();
            _cropMarketSimulation.RunDailyUpdateIfNeeded();
            _fishMarketSimulation.RunDailyUpdateIfNeeded();
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            _ = sender;

            if (e.NewMenu is not StardewValley.Menus.ShopMenu shop)
                return;

            DeferOneTick(() =>
            {
                _shopEditor.Apply(shop);
            });
        }

        private void DeferOneTick(Action action)
        {
            void ApplyNextTick(object? s, UpdateTickedEventArgs args)
            {
                _ = s;
                _ = args;

                this.Helper.Events.GameLoop.UpdateTicked -= ApplyNextTick;

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    this.Monitor.Log($"Failed to apply shop changes: {e}", LogLevel.Error);
                }
            }

            this.Helper.Events.GameLoop.UpdateTicked += ApplyNextTick;
        }

        private string GetItemName(ISalable s)
        {
            if (s is StardewValley.Object obj)
                return obj.Name;

            if (s is StardewValley.Item it)
                return it.Name;

            return s?.ToString() ?? "<unknown>";
        }
    }
}
