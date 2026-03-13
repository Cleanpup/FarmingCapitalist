using System;
using System.Globalization;
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
        internal static ModConfig Config { get; private set; } = new();

        private ShopEditor _shopEditor = null!;
        private CropEconomyCsvExporter _cropEconomyCsvExporter = null!;

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();
            VerbosePriceTraceLogger.Initialize(this.Monitor, Config.EnableVerbosePriceTrace);
            SaveEconomyProfileService.Initialize(helper, this.Monitor);
            CropSupplyDataService.Initialize(helper, this.Monitor);
            CropSupplyModifierService.Initialize(Config.ApplySupplyDemandSellModifier);

            _shopEditor = new ShopEditor(helper, this.Monitor);
            _cropEconomyCsvExporter = new CropEconomyCsvExporter(helper, this.Monitor);

            helper.ConsoleCommands.Add(
                "starecon_dump",
                "Export crop economy debug CSV with modified seed/sell prices and 50-tile profit.",
                this.OnStareconDumpCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_supply_dump",
                "Dump tracked crop supply scores and their current supply modifiers for this save.",
                this.OnStareconSupplyDumpCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_supply_modifier",
                "Show the current supply score and modifier for a crop produce item ID or exact crop name.",
                this.OnStareconSupplyModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_supply_set_modifier",
                "Set a debug override for the supply/demand sell modifier. Allowed range: 0.60 to 1.15.",
                this.OnStareconSupplySetModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_supply_clear_modifier",
                "Clear the debug override for the supply/demand sell modifier.",
                this.OnStareconSupplyClearModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_supply_show_modifier_override",
                "Show the current debug override for the supply/demand sell modifier, if any.",
                this.OnStareconSupplyShowModifierOverrideCommand
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

            this.Monitor.Log("Game launched with Farming Capitalist!", LogLevel.Info);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            _ = sender;
            _ = e;

            SaveEconomyProfileService.LoadOrCreateForCurrentSave();
            CropSupplyDataService.LoadOrCreateForCurrentSave();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            _ = sender;
            _ = e;

            SaveEconomyProfileService.ClearActiveProfile();
            CropSupplyDataService.ClearActiveData();
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

        private void OnStareconSupplyDumpCommand(string command, string[] args)
        {
            _ = command;
            _ = args;

            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save before running starecon_supply_dump.", LogLevel.Warn);
                return;
            }

            IReadOnlyDictionary<string, float> supplyScores = CropSupplyDataService.GetSnapshot();
            if (supplyScores.Count == 0)
            {
                this.Monitor.Log("No crop supply scores are currently tracked for this save.", LogLevel.Info);
                return;
            }

            this.Monitor.Log($"Tracked crop supply scores ({supplyScores.Count} crops):", LogLevel.Info);
            foreach (KeyValuePair<string, float> pair in supplyScores.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
            {
                string displayName = CropSupplyTracker.GetCropDisplayName(pair.Key);
                this.Monitor.Log(CropSupplyModifierService.GetDebugSummary(pair.Key, displayName), LogLevel.Info);
            }
        }

        private void OnStareconSupplyModifierCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save before running starecon_supply_modifier.", LogLevel.Warn);
                return;
            }

            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: starecon_supply_modifier <crop item id or exact crop name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            if (!CropSupplyTracker.TryResolveCropProduceItemId(query, out string produceItemId, out string displayName))
            {
                this.Monitor.Log(
                    $"Could not resolve '{query}' to a crop produce item. Use an exact crop name or produce item ID.",
                    LogLevel.Warn
                );
                return;
            }

            this.Monitor.Log(CropSupplyModifierService.GetDebugSummary(produceItemId, displayName), LogLevel.Info);
        }

        private void OnStareconSupplySetModifierCommand(string command, string[] args)
        {
            _ = command;

            if (args.Length != 1)
            {
                this.Monitor.Log(
                    $"Usage: starecon_supply_set_modifier <value between {CropSupplyModifierService.MinimumAllowedSellModifier:0.###} and {CropSupplyModifierService.MaximumAllowedSellModifier:0.###}>",
                    LogLevel.Warn
                );
                return;
            }

            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float modifier))
            {
                this.Monitor.Log($"Could not parse '{args[0]}' as a numeric modifier.", LogLevel.Error);
                return;
            }

            if (!CropSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out string error))
            {
                this.Monitor.Log(error, LogLevel.Error);
                return;
            }

            this.Monitor.Log(
                $"Supply/demand modifier override set to x{modifier:0.###}. This now replaces the computed supply modifier for crop items.",
                LogLevel.Info
            );
        }

        private void OnStareconSupplyClearModifierCommand(string command, string[] args)
        {
            _ = command;
            _ = args;

            CropSupplyModifierService.ClearDebugSellModifierOverride();
            this.Monitor.Log("Cleared the supply/demand modifier override.", LogLevel.Info);
        }

        private void OnStareconSupplyShowModifierOverrideCommand(string command, string[] args)
        {
            _ = command;
            _ = args;

            if (CropSupplyModifierService.TryGetDebugSellModifierOverride(out float modifier))
            {
                this.Monitor.Log($"Supply/demand modifier override is active at x{modifier:0.###}.", LogLevel.Info);
                return;
            }

            this.Monitor.Log("No supply/demand modifier override is active.", LogLevel.Info);
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
            {
                this.Monitor.Log("Clearing frozen overnight sell context on DayStarted.", LogLevel.Trace);
            }

            EconomyPatches.FrozenOvernightSellContext = null;
            DailyPurchaseTracker.ResetForNewDay();
            CropSupplyDataService.ApplyDailyDecayIfNeeded();
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            _ = sender;

            if (e.NewMenu is not StardewValley.Menus.ShopMenu shop)
                return;

            DeferOneTick(() => _shopEditor.Apply(shop));
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
