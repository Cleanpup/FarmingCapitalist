using System;
using System.Globalization;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    internal sealed class SupplyDebugCommandService
    {
        private readonly IMonitor _monitor;

        public SupplyDebugCommandService(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Register(IModHelper helper)
        {
            helper.ConsoleCommands.Add(
                "starecon_s_dump",
                "Dump tracked supply scores and modifiers. Usage: starecon_s_dump [crop|fish|mining|animal|forage|artisan|monster|all].",
                this.OnStareconSupplyDumpCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_mod",
                "Show the current supply score and modifier. Usage: starecon_s_mod [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name>.",
                this.OnStareconSupplyModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_add",
                "Add tracked supply for debugging. Usage: starecon_s_add [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name> <amount>.",
                this.OnStareconSupplyAddCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_reset",
                "Clear tracked supply scores. Usage: starecon_s_reset [crop|fish|mining|animal|forage|artisan|monster|all].",
                this.OnStareconSupplyResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_decay",
                "Apply category-specific debug decay. Usage: starecon_s_decay [fish|mining|animal|forage|artisan|monster] [days].",
                this.OnStareconSupplyDecayCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_set",
                "Set a debug override for the supply/demand sell modifier. Usage: starecon_s_set [crop|fish|mining|animal|forage|artisan|monster] <value>.",
                this.OnStareconSupplySetModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_clear",
                "Clear the debug override for the supply/demand sell modifier. Usage: starecon_s_clear [crop|fish|mining|animal|forage|artisan|monster|all].",
                this.OnStareconSupplyClearModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_show",
                "Show the current debug override for the supply/demand sell modifier, if any. Usage: starecon_s_show [crop|fish|mining|animal|forage|artisan|monster|all].",
                this.OnStareconSupplyShowModifierOverrideCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_fm_show",
                "Show fish market simulation state.",
                this.OnStareconFishMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_mm_show",
                "Show mining market simulation state.",
                this.OnStareconMineralMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_am_show",
                "Show animal product market simulation state.",
                this.OnStareconAnimalProductMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_am_reset",
                "Reset animal product supply and market simulation state.",
                this.OnStareconAnimalProductMarketResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_am_price",
                "Show the current animal product buy/sell modifiers. Usage: starecon_am_price <item id or exact name>.",
                this.OnStareconAnimalProductPriceCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_fg_show",
                "Show forageable market simulation state.",
                this.OnStareconForageableMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_fg_reset",
                "Reset forageable supply and market simulation state.",
                this.OnStareconForageableMarketResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_fg_price",
                "Show the current forageable buy/sell modifiers. Usage: starecon_fg_price <item id or exact name>.",
                this.OnStareconForageablePriceCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ag_show",
                "Show artisan good market simulation state.",
                this.OnStareconArtisanGoodMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ag_reset",
                "Reset artisan good supply and market simulation state.",
                this.OnStareconArtisanGoodMarketResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ag_price",
                "Show the current artisan good buy/sell modifiers. Usage: starecon_ag_price <item id or exact name>.",
                this.OnStareconArtisanGoodPriceCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ml_show",
                "Show monster loot market simulation state.",
                this.OnStareconMonsterLootMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ml_reset",
                "Reset monster loot supply and market simulation state.",
                this.OnStareconMonsterLootMarketResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_ml_price",
                "Show the current monster loot buy/sell modifiers. Usage: starecon_ml_price <item id or exact name>.",
                this.OnStareconMonsterLootPriceCommand
            );
        }

        private void OnStareconSupplyDumpCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_s_dump.", LogLevel.Warn);
                return;
            }

            if (args.Length > 1)
            {
                _monitor.Log("Usage: starecon_s_dump [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_dump [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            bool wroteAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Mineral);

            if (scope is SupplyDebugScope.AnimalProduct or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.AnimalProduct);

            if (scope is SupplyDebugScope.Forageable or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Forageable);

            if (scope is SupplyDebugScope.ArtisanGood or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.ArtisanGood);

            if (scope is SupplyDebugScope.MonsterLoot or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.MonsterLoot);

            if (!wroteAny)
                _monitor.Log("No supply scores are currently tracked for the requested category.", LogLevel.Info);
        }

        private void OnStareconSupplyModifierCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_s_mod.", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyCategory(args, defaultCategory: SupplyDebugCategory.Crop, out SupplyDebugCategory category, out int argIndex))
            {
                _monitor.Log("Usage: starecon_s_mod [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name>", LogLevel.Warn);
                return;
            }

            if (argIndex >= args.Length)
            {
                _monitor.Log("Usage: starecon_s_mod [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args.Skip(argIndex));
            if (!TryResolveSupplyItem(category, query, out string itemId, out string displayName))
            {
                _monitor.Log(GetSupplyResolveFailureMessage(category, query), LogLevel.Warn);
                return;
            }

            _monitor.Log(GetSupplyDebugSummary(category, itemId, displayName), LogLevel.Info);
        }

        private void OnStareconSupplyAddCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_s_add.", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyCategory(args, defaultCategory: SupplyDebugCategory.Crop, out SupplyDebugCategory category, out int argIndex))
            {
                _monitor.Log("Usage: starecon_s_add [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name> <amount>", LogLevel.Warn);
                return;
            }

            int amountIndex = args.Length - 1;
            if (amountIndex < argIndex)
            {
                _monitor.Log("Usage: starecon_s_add [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name> <amount>", LogLevel.Warn);
                return;
            }

            if (!float.TryParse(args[amountIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) || amount <= 0f)
            {
                _monitor.Log($"Could not parse '{args[amountIndex]}' as a positive numeric supply amount.", LogLevel.Error);
                return;
            }

            string query = string.Join(" ", args.Skip(argIndex).Take(amountIndex - argIndex));
            if (string.IsNullOrWhiteSpace(query))
            {
                _monitor.Log("Usage: starecon_s_add [crop|fish|mining|animal|forage|artisan|monster] <item id or exact name> <amount>", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyItem(category, query, out string itemId, out string displayName))
            {
                _monitor.Log(GetSupplyResolveFailureMessage(category, query), LogLevel.Warn);
                return;
            }

            float updatedScore = AddSupplyForCategory(category, itemId, displayName, amount);
            float modifier = GetSupplyModifierForCategory(category, itemId, displayName);
            _monitor.Log(
                $"Added {amount:0.##} {GetSupplyCategoryLabel(category)} supply for {displayName} ({itemId}). New score: {updatedScore:0.##}, modifier x{modifier:0.###}.",
                LogLevel.Info
            );
        }

        private void OnStareconSupplyResetCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_s_reset.", LogLevel.Warn);
                return;
            }

            if (args.Length > 1)
            {
                _monitor.Log("Usage: starecon_s_reset [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_reset [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            bool resetAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
            {
                if (CropSupplyDataService.GetSnapshot().Count > 0)
                {
                    CropSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
            {
                if (FishSupplyDataService.GetSnapshot().Count > 0)
                {
                    FishSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
            {
                if (MineralSupplyDataService.GetSnapshot().Count > 0)
                {
                    MineralSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.AnimalProduct or SupplyDebugScope.All)
            {
                if (AnimalProductSupplyDataService.GetSnapshot().Count > 0)
                {
                    AnimalProductSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.Forageable or SupplyDebugScope.All)
            {
                if (ForageableSupplyDataService.GetSnapshot().Count > 0)
                {
                    ForageableSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.ArtisanGood or SupplyDebugScope.All)
            {
                if (ArtisanGoodSupplyDataService.GetSnapshot().Count > 0)
                {
                    ArtisanGoodSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (scope is SupplyDebugScope.MonsterLoot or SupplyDebugScope.All)
            {
                if (MonsterLootSupplyDataService.GetSnapshot().Count > 0)
                {
                    MonsterLootSupplyDataService.ResetTrackedSupply();
                    resetAny = true;
                }
            }

            if (!resetAny)
                _monitor.Log("No supply scores are currently tracked for the requested category.", LogLevel.Info);
        }

        private void OnStareconSupplyDecayCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_s_decay.", LogLevel.Warn);
                return;
            }

            SupplyDebugCategory category = SupplyDebugCategory.Fish;
            int argIndex = 0;
            if (args.Length > 0 && TryParseSupplyCategory(args[0], out SupplyDebugCategory explicitCategory))
            {
                category = explicitCategory;
                argIndex = 1;
            }

            if (category is not SupplyDebugCategory.Fish
                && category is not SupplyDebugCategory.Mineral
                && category is not SupplyDebugCategory.AnimalProduct
                && category is not SupplyDebugCategory.Forageable
                && category is not SupplyDebugCategory.ArtisanGood
                && category is not SupplyDebugCategory.MonsterLoot)
            {
                _monitor.Log("Crop supply does not expose a direct decay command. Use starecon_s_decay [fish|mining|animal|forage|artisan|monster] [days].", LogLevel.Warn);
                return;
            }

            if (args.Length - argIndex > 1)
            {
                _monitor.Log("Usage: starecon_s_decay [fish|mining|animal|forage|artisan|monster] [days]", LogLevel.Warn);
                return;
            }

            int days = 1;
            if (argIndex < args.Length && (!int.TryParse(args[argIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out days) || days <= 0))
            {
                _monitor.Log($"Could not parse '{args[argIndex]}' as a positive day count.", LogLevel.Error);
                return;
            }

            bool changed = category switch
            {
                SupplyDebugCategory.Fish => FishMarketSimulationService.ApplyDebugDailyUpdate(days),
                SupplyDebugCategory.Mineral => MineralMarketSimulationService.ApplyDebugDailyUpdate(days),
                SupplyDebugCategory.AnimalProduct => AnimalProductMarketSimulationService.ApplyDebugDailyUpdate(days),
                SupplyDebugCategory.Forageable => ForageableMarketSimulationService.ApplyDebugDailyUpdate(days),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodMarketSimulationService.ApplyDebugDailyUpdate(days),
                SupplyDebugCategory.MonsterLoot => MonsterLootMarketSimulationService.ApplyDebugDailyUpdate(days),
                _ => false
            };
            if (!changed)
            {
                _monitor.Log(
                    $"{char.ToUpperInvariant(GetSupplyCategoryLabel(category)[0])}{GetSupplyCategoryLabel(category).Substring(1)} supply decay made no changes. Either no tracked {GetSupplyCategoryPlural(category)} exist yet, the tracked {GetSupplyCategoryPlural(category)} are already neutral, or the save is not ready.",
                    LogLevel.Info
                );
                return;
            }

            _monitor.Log(
                $"{char.ToUpperInvariant(GetSupplyCategoryLabel(category)[0])}{GetSupplyCategoryLabel(category).Substring(1)} supply decay simulated {days} day(s).",
                LogLevel.Info
            );
        }

        private void OnStareconSupplySetModifierCommand(string command, string[] args)
        {
            _ = command;

            if (!TryResolveSupplyCategory(args, defaultCategory: SupplyDebugCategory.Crop, out SupplyDebugCategory category, out int argIndex))
            {
                _monitor.Log(
                    $"Usage: starecon_s_set [crop|fish|mining|animal|forage|artisan|monster] <value between {CropSupplyModifierService.MinimumAllowedSellModifier:0.###} and {CropSupplyModifierService.MaximumAllowedSellModifier:0.###}>",
                    LogLevel.Warn
                );
                return;
            }

            if (args.Length - argIndex != 1)
            {
                _monitor.Log(
                    $"Usage: starecon_s_set [crop|fish|mining|animal|forage|artisan|monster] <value between {CropSupplyModifierService.MinimumAllowedSellModifier:0.###} and {CropSupplyModifierService.MaximumAllowedSellModifier:0.###}>",
                    LogLevel.Warn
                );
                return;
            }

            if (!float.TryParse(args[argIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float modifier))
            {
                _monitor.Log($"Could not parse '{args[argIndex]}' as a numeric modifier.", LogLevel.Error);
                return;
            }

            if (!TrySetSupplyModifierOverride(category, modifier, out string error))
            {
                _monitor.Log(error, LogLevel.Error);
                return;
            }

            ResetTrackedSupplyForCategory(category);
            _monitor.Log(
                $"Supply/demand modifier override for {GetSupplyCategoryLabel(category)} set to x{modifier:0.###}. Tracked {GetSupplyCategoryLabel(category)} supply was reset, and supply tracking will stay suspended until the override is cleared.",
                LogLevel.Info
            );
        }

        private void OnStareconSupplyClearModifierCommand(string command, string[] args)
        {
            _ = command;

            if (args.Length > 1)
            {
                _monitor.Log("Usage: starecon_s_clear [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_clear [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            bool clearedAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Mineral);

            if (scope is SupplyDebugScope.AnimalProduct or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.AnimalProduct);

            if (scope is SupplyDebugScope.Forageable or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Forageable);

            if (scope is SupplyDebugScope.ArtisanGood or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.ArtisanGood);

            if (scope is SupplyDebugScope.MonsterLoot or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.MonsterLoot);

            if (clearedAny)
                return;

            _monitor.Log("No supply/demand modifier override is active for the requested category.", LogLevel.Info);
        }

        private void OnStareconSupplyShowModifierOverrideCommand(string command, string[] args)
        {
            _ = command;

            if (args.Length > 1)
            {
                _monitor.Log("Usage: starecon_s_show [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_show [crop|fish|mining|animal|forage|artisan|monster|all]", LogLevel.Warn);
                return;
            }

            bool showedAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Mineral);

            if (scope is SupplyDebugScope.AnimalProduct or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.AnimalProduct);

            if (scope is SupplyDebugScope.Forageable or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Forageable);

            if (scope is SupplyDebugScope.ArtisanGood or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.ArtisanGood);

            if (scope is SupplyDebugScope.MonsterLoot or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.MonsterLoot);

            if (!showedAny)
                _monitor.Log("No supply/demand modifier override is active for the requested category.", LogLevel.Info);
        }

        private void OnStareconFishMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_fm_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_fm_show", LogLevel.Warn);
                return;
            }

            foreach (string line in FishMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconMineralMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_mm_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_mm_show", LogLevel.Warn);
                return;
            }

            foreach (string line in MineralMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconAnimalProductMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_am_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_am_show", LogLevel.Warn);
                return;
            }

            foreach (string line in AnimalProductMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconAnimalProductMarketResetCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_am_reset.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_am_reset", LogLevel.Warn);
                return;
            }

            AnimalProductSupplyDataService.ResetTrackedSupply();
            AnimalProductMarketSimulationService.ResetSimulationState();
            _monitor.Log("Reset animal product supply and market simulation state.", LogLevel.Info);
        }

        private void OnStareconAnimalProductPriceCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_am_price.", LogLevel.Warn);
                return;
            }

            if (args.Length == 0)
            {
                _monitor.Log("Usage: starecon_am_price <item id or exact name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            if (!AnimalProductSupplyTracker.TryResolveAnimalProductItemId(query, out string itemId, out string displayName))
            {
                _monitor.Log($"Could not resolve '{query}' to an animal product item. Use an exact animal-product name or item ID.", LogLevel.Warn);
                return;
            }

            if (!AnimalProductEconomyItemRules.TryCreateAnimalProductObject(itemId, out StardewValley.Object? animalProductObject)
                || animalProductObject is null)
            {
                _monitor.Log($"Failed to create an animal product item instance for '{displayName}' ({itemId}).", LogLevel.Error);
                return;
            }

            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: _monitor);
            float sellTraitModifier = AnimalProductTraitEconomyRules.GetSellTraitModifier(animalProductObject, context);
            float sellSupplyModifier = AnimalProductSupplyModifierService.GetSellModifier(itemId, displayName);
            float totalSellModifier = sellTraitModifier * sellSupplyModifier;
            float buyTraitModifier = AnimalProductTraitEconomyRules.GetBuyTraitModifier(animalProductObject, context);

            _monitor.Log(
                $"Animal product price modifiers for {displayName} ({itemId}): sell trait x{sellTraitModifier:0.###}, sell supply x{sellSupplyModifier:0.###}, total sell x{totalSellModifier:0.###}, buy trait x{buyTraitModifier:0.###}, season {context.Season}.",
                LogLevel.Info
            );
        }

        private void OnStareconForageableMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_fg_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_fg_show", LogLevel.Warn);
                return;
            }

            foreach (string line in ForageableMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconForageableMarketResetCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_fg_reset.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_fg_reset", LogLevel.Warn);
                return;
            }

            ForageableSupplyDataService.ResetTrackedSupply();
            ForageableMarketSimulationService.ResetSimulationState();
            _monitor.Log("Reset forageable supply and market simulation state.", LogLevel.Info);
        }

        private void OnStareconForageablePriceCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_fg_price.", LogLevel.Warn);
                return;
            }

            if (args.Length == 0)
            {
                _monitor.Log("Usage: starecon_fg_price <item id or exact name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            if (!ForageableSupplyTracker.TryResolveForageableItemId(query, out string itemId, out string displayName))
            {
                _monitor.Log($"Could not resolve '{query}' to a forageable item. Use an exact forageable name or item ID.", LogLevel.Warn);
                return;
            }

            if (!ForageableEconomyItemRules.TryCreateForageableObject(itemId, out StardewValley.Object? forageableObject)
                || forageableObject is null)
            {
                _monitor.Log($"Failed to create a forageable item instance for '{displayName}' ({itemId}).", LogLevel.Error);
                return;
            }

            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: _monitor);
            float sellTraitModifier = ForageableTraitEconomyRules.GetSellTraitModifier(forageableObject, context);
            float sellSupplyModifier = ForageableSupplyModifierService.GetSellModifier(itemId, displayName);
            float totalSellModifier = sellTraitModifier * sellSupplyModifier;
            float buyTraitModifier = ForageableTraitEconomyRules.GetBuyTraitModifier(forageableObject, context);

            _monitor.Log(
                $"Forageable price modifiers for {displayName} ({itemId}): sell trait x{sellTraitModifier:0.###}, sell supply x{sellSupplyModifier:0.###}, total sell x{totalSellModifier:0.###}, buy trait x{buyTraitModifier:0.###}, season {context.Season}.",
                LogLevel.Info
            );
        }

        private void OnStareconArtisanGoodMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ag_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_ag_show", LogLevel.Warn);
                return;
            }

            foreach (string line in ArtisanGoodMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconArtisanGoodMarketResetCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ag_reset.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_ag_reset", LogLevel.Warn);
                return;
            }

            ArtisanGoodSupplyDataService.ResetTrackedSupply();
            ArtisanGoodMarketSimulationService.ResetSimulationState();
            _monitor.Log("Reset artisan good supply and market simulation state.", LogLevel.Info);
        }

        private void OnStareconArtisanGoodPriceCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ag_price.", LogLevel.Warn);
                return;
            }

            if (args.Length == 0)
            {
                _monitor.Log("Usage: starecon_ag_price <item id or exact name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            if (!ArtisanGoodSupplyTracker.TryResolveArtisanGoodItemId(query, out string itemId, out string displayName))
            {
                _monitor.Log($"Could not resolve '{query}' to an artisan good item. Use an exact artisan-good name or item ID.", LogLevel.Warn);
                return;
            }

            if (!ArtisanGoodEconomyItemRules.TryCreateArtisanGoodObject(itemId, out StardewValley.Object? artisanGoodObject)
                || artisanGoodObject is null)
            {
                _monitor.Log($"Failed to create an artisan good item instance for '{displayName}' ({itemId}).", LogLevel.Error);
                return;
            }

            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: _monitor);
            float sellTraitModifier = ArtisanGoodTraitEconomyRules.GetSellTraitModifier(artisanGoodObject, context);
            float sellSupplyModifier = ArtisanGoodSupplyModifierService.GetSellModifier(itemId, displayName);
            float totalSellModifier = sellTraitModifier * sellSupplyModifier;
            float buyTraitModifier = ArtisanGoodTraitEconomyRules.GetBuyTraitModifier(artisanGoodObject, context);

            _monitor.Log(
                $"Artisan good price modifiers for {displayName} ({itemId}): sell trait x{sellTraitModifier:0.###}, sell supply x{sellSupplyModifier:0.###}, total sell x{totalSellModifier:0.###}, buy trait x{buyTraitModifier:0.###}, season {context.Season}.",
                LogLevel.Info
            );
        }

        private void OnStareconMonsterLootMarketShowCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ml_show.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_ml_show", LogLevel.Warn);
                return;
            }

            foreach (string line in MonsterLootMarketSimulationService.GetDebugStatusLines())
                _monitor.Log(line, LogLevel.Info);
        }

        private void OnStareconMonsterLootMarketResetCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ml_reset.", LogLevel.Warn);
                return;
            }

            if (args.Length > 0)
            {
                _monitor.Log("Usage: starecon_ml_reset", LogLevel.Warn);
                return;
            }

            MonsterLootSupplyDataService.ResetTrackedSupply();
            MonsterLootMarketSimulationService.ResetSimulationState();
            _monitor.Log("Reset monster loot supply and market simulation state.", LogLevel.Info);
        }

        private void OnStareconMonsterLootPriceCommand(string command, string[] args)
        {
            _ = command;

            if (!Context.IsWorldReady)
            {
                _monitor.Log("Load a save before running starecon_ml_price.", LogLevel.Warn);
                return;
            }

            if (args.Length == 0)
            {
                _monitor.Log("Usage: starecon_ml_price <item id or exact name>", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            if (!MonsterLootSupplyTracker.TryResolveMonsterLootItemId(query, out string itemId, out string displayName))
            {
                _monitor.Log($"Could not resolve '{query}' to a monster loot item. Use an exact monster-loot name or item ID.", LogLevel.Warn);
                return;
            }

            if (!MonsterLootEconomyItemRules.TryCreateMonsterLootObject(itemId, out StardewValley.Object? monsterLootObject)
                || monsterLootObject is null)
            {
                _monitor.Log($"Failed to create a monster loot item instance for '{displayName}' ({itemId}).", LogLevel.Error);
                return;
            }

            EconomyContext context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: _monitor);
            float sellTraitModifier = MonsterLootTraitEconomyRules.GetSellTraitModifier(monsterLootObject, context);
            float sellSupplyModifier = MonsterLootSupplyModifierService.GetSellModifier(itemId, displayName);
            float totalSellModifier = sellTraitModifier * sellSupplyModifier;
            float buyTraitModifier = MonsterLootTraitEconomyRules.GetBuyTraitModifier(monsterLootObject, context);

            _monitor.Log(
                $"Monster loot price modifiers for {displayName} ({itemId}): sell trait x{sellTraitModifier:0.###}, sell supply x{sellSupplyModifier:0.###}, total sell x{totalSellModifier:0.###}, buy trait x{buyTraitModifier:0.###}, season {context.Season}.",
                LogLevel.Info
            );
        }

        private bool DumpSupplyForCategory(SupplyDebugCategory category)
        {
            IReadOnlyDictionary<string, float> supplyScores = GetSupplySnapshot(category);
            if (supplyScores.Count == 0)
                return false;

            string categoryLabel = GetSupplyCategoryLabel(category);
            _monitor.Log(
                $"Tracked {categoryLabel} supply scores ({supplyScores.Count} {GetSupplyCategoryPlural(category)}):",
                LogLevel.Info
            );

            foreach (KeyValuePair<string, float> pair in supplyScores.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
            {
                string displayName = GetSupplyDisplayName(category, pair.Key);
                _monitor.Log(GetSupplyDebugSummary(category, pair.Key, displayName), LogLevel.Info);
            }

            return true;
        }

        private static IReadOnlyDictionary<string, float> GetSupplySnapshot(SupplyDebugCategory category)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.Fish => FishSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.Mineral => MineralSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.Forageable => ForageableSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyDataService.GetSnapshot(),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyDataService.GetSnapshot(),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetSupplyDisplayName(SupplyDebugCategory category, string itemId)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyTracker.GetCropDisplayName(itemId),
                SupplyDebugCategory.Fish => FishSupplyTracker.GetFishDisplayName(itemId),
                SupplyDebugCategory.Mineral => MineralSupplyTracker.GetMineralDisplayName(itemId),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyTracker.GetAnimalProductDisplayName(itemId),
                SupplyDebugCategory.Forageable => ForageableSupplyTracker.GetForageableDisplayName(itemId),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName(itemId),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyTracker.GetMonsterLootDisplayName(itemId),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetSupplyDebugSummary(SupplyDebugCategory category, string itemId, string displayName)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.Fish => FishSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.Mineral => MineralSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.Forageable => ForageableSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyModifierService.GetDebugSummary(itemId, displayName),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyModifierService.GetDebugSummary(itemId, displayName),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static float AddSupplyForCategory(SupplyDebugCategory category, string itemId, string displayName, float amount)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.Fish => FishSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.Mineral => MineralSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.Forageable => ForageableSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyDataService.AddSupply(itemId, amount, displayName, "debug-command"),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static float GetSupplyModifierForCategory(SupplyDebugCategory category, string itemId, string displayName)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.Fish => FishSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.Mineral => MineralSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.Forageable => ForageableSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyModifierService.GetSellModifier(itemId, displayName),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyModifierService.GetSellModifier(itemId, displayName),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static bool TrySetSupplyModifierOverride(SupplyDebugCategory category, float modifier, out string error)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.Fish => FishSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.Mineral => MineralSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.Forageable => ForageableSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyModifierService.TrySetDebugSellModifierOverride(modifier, out error),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static void ResetTrackedSupplyForCategory(SupplyDebugCategory category)
        {
            if (category == SupplyDebugCategory.Crop)
            {
                CropSupplyDataService.ResetTrackedSupply();
                return;
            }

            if (category == SupplyDebugCategory.Fish)
            {
                FishSupplyDataService.ResetTrackedSupply();
                return;
            }

            if (category == SupplyDebugCategory.Mineral)
            {
                MineralSupplyDataService.ResetTrackedSupply();
                return;
            }

            if (category == SupplyDebugCategory.AnimalProduct)
            {
                AnimalProductSupplyDataService.ResetTrackedSupply();
                return;
            }

            if (category == SupplyDebugCategory.Forageable)
            {
                ForageableSupplyDataService.ResetTrackedSupply();
                return;
            }

            if (category == SupplyDebugCategory.ArtisanGood)
            {
                ArtisanGoodSupplyDataService.ResetTrackedSupply();
                return;
            }

            MonsterLootSupplyDataService.ResetTrackedSupply();
        }

        private bool ClearSupplyModifierOverride(SupplyDebugCategory category)
        {
            if (!TryGetSupplyModifierOverride(category, out _))
                return false;

            if (category == SupplyDebugCategory.Crop)
                CropSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.Fish)
                FishSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.Mineral)
                MineralSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.AnimalProduct)
                AnimalProductSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.Forageable)
                ForageableSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.ArtisanGood)
                ArtisanGoodSupplyModifierService.ClearDebugSellModifierOverride();
            else
                MonsterLootSupplyModifierService.ClearDebugSellModifierOverride();

            _monitor.Log($"Cleared the {GetSupplyCategoryLabel(category)} supply/demand modifier override.", LogLevel.Info);
            return true;
        }

        private bool ShowSupplyModifierOverride(SupplyDebugCategory category)
        {
            if (!TryGetSupplyModifierOverride(category, out float modifier))
                return false;

            _monitor.Log($"{GetSupplyCategoryLabel(category)} supply/demand modifier override is active at x{modifier:0.###}.", LogLevel.Info);
            return true;
        }

        private static bool TryGetSupplyModifierOverride(SupplyDebugCategory category, out float modifier)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.Fish => FishSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.Mineral => MineralSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.Forageable => ForageableSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyModifierService.TryGetDebugSellModifierOverride(out modifier),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static bool TryResolveSupplyItem(SupplyDebugCategory category, string query, out string itemId, out string displayName)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => CropSupplyTracker.TryResolveCropProduceItemId(query, out itemId, out displayName),
                SupplyDebugCategory.Fish => FishSupplyTracker.TryResolveFishItemId(query, out itemId, out displayName),
                SupplyDebugCategory.Mineral => MineralSupplyTracker.TryResolveMineralItemId(query, out itemId, out displayName),
                SupplyDebugCategory.AnimalProduct => AnimalProductSupplyTracker.TryResolveAnimalProductItemId(query, out itemId, out displayName),
                SupplyDebugCategory.Forageable => ForageableSupplyTracker.TryResolveForageableItemId(query, out itemId, out displayName),
                SupplyDebugCategory.ArtisanGood => ArtisanGoodSupplyTracker.TryResolveArtisanGoodItemId(query, out itemId, out displayName),
                SupplyDebugCategory.MonsterLoot => MonsterLootSupplyTracker.TryResolveMonsterLootItemId(query, out itemId, out displayName),
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetSupplyResolveFailureMessage(SupplyDebugCategory category, string query)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => $"Could not resolve '{query}' to a crop produce item. Use an exact crop name or produce item ID.",
                SupplyDebugCategory.Fish => $"Could not resolve '{query}' to a fish item. Use an exact fish name or fish item ID.",
                SupplyDebugCategory.Mineral => $"Could not resolve '{query}' to a mining item. Use an exact mining-item name or item ID.",
                SupplyDebugCategory.AnimalProduct => $"Could not resolve '{query}' to an animal product item. Use an exact animal-product name or item ID.",
                SupplyDebugCategory.Forageable => $"Could not resolve '{query}' to a forageable item. Use an exact forageable name or item ID.",
                SupplyDebugCategory.ArtisanGood => $"Could not resolve '{query}' to an artisan good item. Use an exact artisan-good name or item ID.",
                SupplyDebugCategory.MonsterLoot => $"Could not resolve '{query}' to a monster loot item. Use an exact monster-loot name or item ID.",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static bool TryResolveSupplyCategory(
            string[] args,
            SupplyDebugCategory defaultCategory,
            out SupplyDebugCategory category,
            out int argIndex
        )
        {
            if (args.Length > 0 && TryParseSupplyCategory(args[0], out category))
            {
                argIndex = 1;
                return true;
            }

            category = defaultCategory;
            argIndex = 0;
            return true;
        }

        private static bool TryResolveSupplyScope(string[] args, SupplyDebugScope defaultScope, out SupplyDebugScope scope)
        {
            if (args.Length == 0)
            {
                scope = defaultScope;
                return true;
            }

            return TryParseSupplyScope(args[0], out scope);
        }

        private static bool TryParseSupplyCategory(string? raw, out SupplyDebugCategory category)
        {
            if (string.Equals(raw, "crop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "crops", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.Crop;
                return true;
            }

            if (string.Equals(raw, "fish", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.Fish;
                return true;
            }

            if (string.Equals(raw, "mineral", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "minerals", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "mining", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.Mineral;
                return true;
            }

            if (string.Equals(raw, "animal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "animals", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "animalproduct", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "animalproducts", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "ap", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.AnimalProduct;
                return true;
            }

            if (string.Equals(raw, "forage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "foraging", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "forageable", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "forageables", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "fg", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.Forageable;
                return true;
            }

            if (string.Equals(raw, "artisan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "artisangood", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "artisangoods", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "ag", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.ArtisanGood;
                return true;
            }

            if (string.Equals(raw, "monster", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "loot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "monsterloot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "monster-loot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "ml", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.MonsterLoot;
                return true;
            }

            category = default;
            return false;
        }

        private static bool TryParseSupplyScope(string? raw, out SupplyDebugScope scope)
        {
            if (string.Equals(raw, "all", StringComparison.OrdinalIgnoreCase))
            {
                scope = SupplyDebugScope.All;
                return true;
            }

            if (TryParseSupplyCategory(raw, out SupplyDebugCategory category))
            {
                scope = category == SupplyDebugCategory.Crop
                    ? SupplyDebugScope.Crop
                    : category == SupplyDebugCategory.Fish
                        ? SupplyDebugScope.Fish
                        : category == SupplyDebugCategory.Mineral
                            ? SupplyDebugScope.Mineral
                            : category == SupplyDebugCategory.AnimalProduct
                                ? SupplyDebugScope.AnimalProduct
                                : category == SupplyDebugCategory.Forageable
                                    ? SupplyDebugScope.Forageable
                                    : category == SupplyDebugCategory.ArtisanGood
                                        ? SupplyDebugScope.ArtisanGood
                                        : SupplyDebugScope.MonsterLoot;
                return true;
            }

            scope = default;
            return false;
        }

        private static string GetSupplyCategoryLabel(SupplyDebugCategory category)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => "crop",
                SupplyDebugCategory.Fish => "fish",
                SupplyDebugCategory.Mineral => "mining",
                SupplyDebugCategory.AnimalProduct => "animal product",
                SupplyDebugCategory.Forageable => "forageable",
                SupplyDebugCategory.ArtisanGood => "artisan good",
                SupplyDebugCategory.MonsterLoot => "monster loot",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetSupplyCategoryPlural(SupplyDebugCategory category)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => "crops",
                SupplyDebugCategory.Fish => "fish",
                SupplyDebugCategory.Mineral => "mining items",
                SupplyDebugCategory.AnimalProduct => "animal products",
                SupplyDebugCategory.Forageable => "forageables",
                SupplyDebugCategory.ArtisanGood => "artisan goods",
                SupplyDebugCategory.MonsterLoot => "monster-loot items",
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private enum SupplyDebugCategory
        {
            Crop,
            Fish,
            Mineral,
            AnimalProduct,
            Forageable,
            ArtisanGood,
            MonsterLoot
        }

        private enum SupplyDebugScope
        {
            Crop,
            Fish,
            Mineral,
            AnimalProduct,
            Forageable,
            ArtisanGood,
            MonsterLoot,
            All
        }
    }
}
