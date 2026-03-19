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
        private IMarketSimulationLifecycle _mineralMarketSimulation = null!;
        private IMarketSimulationLifecycle _animalProductMarketSimulation = null!;
        private IMarketSimulationLifecycle _forageableMarketSimulation = null!;
        private IMarketSimulationLifecycle _plantExtraMarketSimulation = null!;
        private IMarketSimulationLifecycle _craftingExtraMarketSimulation = null!;
        private IMarketSimulationLifecycle _artisanGoodMarketSimulation = null!;
        private IMarketSimulationLifecycle _cookingFoodMarketSimulation = null!;
        private IMarketSimulationLifecycle _monsterLootMarketSimulation = null!;
        private IMarketSimulationLifecycle _equipmentMarketSimulation = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            FishEconomyItemRules.Initialize(Config.FishClassification);
            VerbosePriceTraceLogger.Initialize(this.Monitor, Config.EnableVerbosePriceTrace);
            SaveEconomyProfileService.Initialize(helper, this.Monitor);
            CropSupplyDataService.Initialize(helper, this.Monitor);
            FishSupplyDataService.Initialize(helper, this.Monitor);
            MineralSupplyDataService.Initialize(helper, this.Monitor);
            AnimalProductSupplyDataService.Initialize(helper, this.Monitor);
            ForageableSupplyDataService.Initialize(helper, this.Monitor);
            PlantExtraSupplyDataService.Initialize(helper, this.Monitor);
            CraftingExtraSupplyDataService.Initialize(helper, this.Monitor);
            ArtisanGoodSupplyDataService.Initialize(helper, this.Monitor);
            CookingFoodSupplyDataService.Initialize(helper, this.Monitor);
            MonsterLootSupplyDataService.Initialize(helper, this.Monitor);
            EquipmentSupplyDataService.Initialize(helper, this.Monitor);
            CropSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            FishSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            MineralSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            AnimalProductSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            ForageableSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            PlantExtraSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            CraftingExtraSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            ArtisanGoodSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            CookingFoodSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            MonsterLootSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);
            EquipmentSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);

            _shopEditor = new ShopEditor(helper, this.Monitor);
            _cropEconomyCsvExporter = new CropEconomyCsvExporter(helper, this.Monitor);
            _supplyDebugCommands = new SupplyDebugCommandService(this.Monitor);
            _cropMarketSimulation = new CropMarketSimulationLifecycleAdapter();
            _fishMarketSimulation = new FishMarketSimulationLifecycleAdapter();
            _mineralMarketSimulation = new MineralMarketSimulationLifecycleAdapter();
            _animalProductMarketSimulation = new AnimalProductMarketSimulationLifecycleAdapter();
            _forageableMarketSimulation = new ForageableMarketSimulationLifecycleAdapter();
            _plantExtraMarketSimulation = new PlantExtraMarketSimulationLifecycleAdapter();
            _craftingExtraMarketSimulation = new CraftingExtraMarketSimulationLifecycleAdapter();
            _artisanGoodMarketSimulation = new ArtisanGoodMarketSimulationLifecycleAdapter();
            _cookingFoodMarketSimulation = new CookingFoodMarketSimulationLifecycleAdapter();
            _monsterLootMarketSimulation = new MonsterLootMarketSimulationLifecycleAdapter();
            _equipmentMarketSimulation = new EquipmentMarketSimulationLifecycleAdapter();
            _cropMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _fishMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _mineralMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _animalProductMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _forageableMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _plantExtraMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _craftingExtraMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _artisanGoodMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _cookingFoodMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _monsterLootMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);
            _equipmentMarketSimulation.Initialize(helper, this.Monitor, Config.Debug.VerboseLogs);

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
            _mineralMarketSimulation.LoadOrCreateForCurrentSave();
            _animalProductMarketSimulation.LoadOrCreateForCurrentSave();
            _forageableMarketSimulation.LoadOrCreateForCurrentSave();
            _plantExtraMarketSimulation.LoadOrCreateForCurrentSave();
            _craftingExtraMarketSimulation.LoadOrCreateForCurrentSave();
            _artisanGoodMarketSimulation.LoadOrCreateForCurrentSave();
            _cookingFoodMarketSimulation.LoadOrCreateForCurrentSave();
            _monsterLootMarketSimulation.LoadOrCreateForCurrentSave();
            _equipmentMarketSimulation.LoadOrCreateForCurrentSave();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            _ = sender;
            _ = e;

            SaveEconomyProfileService.ClearActiveProfile();
            CropSupplyDataService.ClearActiveData();
            _cropMarketSimulation.ClearActiveData();
            _fishMarketSimulation.ClearActiveData();
            _mineralMarketSimulation.ClearActiveData();
            _animalProductMarketSimulation.ClearActiveData();
            _forageableMarketSimulation.ClearActiveData();
            _plantExtraMarketSimulation.ClearActiveData();
            _craftingExtraMarketSimulation.ClearActiveData();
            _artisanGoodMarketSimulation.ClearActiveData();
            _cookingFoodMarketSimulation.ClearActiveData();
            _monsterLootMarketSimulation.ClearActiveData();
            _equipmentMarketSimulation.ClearActiveData();
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
            _mineralMarketSimulation.RunDailyUpdateIfNeeded();
            _animalProductMarketSimulation.RunDailyUpdateIfNeeded();
            _forageableMarketSimulation.RunDailyUpdateIfNeeded();
            _plantExtraMarketSimulation.RunDailyUpdateIfNeeded();
            _craftingExtraMarketSimulation.RunDailyUpdateIfNeeded();
            _artisanGoodMarketSimulation.RunDailyUpdateIfNeeded();
            _cookingFoodMarketSimulation.RunDailyUpdateIfNeeded();
            _monsterLootMarketSimulation.RunDailyUpdateIfNeeded();
            _equipmentMarketSimulation.RunDailyUpdateIfNeeded();
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
