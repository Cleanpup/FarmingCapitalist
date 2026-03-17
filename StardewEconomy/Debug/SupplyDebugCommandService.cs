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
                "Dump tracked supply scores and modifiers. Usage: starecon_s_dump [crop|fish|mineral|all].",
                this.OnStareconSupplyDumpCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_mod",
                "Show the current supply score and modifier. Usage: starecon_s_mod [crop|fish|mineral] <item id or exact name>.",
                this.OnStareconSupplyModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_add",
                "Add tracked supply for debugging. Usage: starecon_s_add [crop|fish|mineral] <item id or exact name> <amount>.",
                this.OnStareconSupplyAddCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_reset",
                "Clear tracked supply scores. Usage: starecon_s_reset [crop|fish|mineral|all].",
                this.OnStareconSupplyResetCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_decay",
                "Apply category-specific debug decay. Usage: starecon_s_decay [fish|mineral] [days].",
                this.OnStareconSupplyDecayCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_set",
                "Set a debug override for the supply/demand sell modifier. Usage: starecon_s_set [crop|fish|mineral] <value>.",
                this.OnStareconSupplySetModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_clear",
                "Clear the debug override for the supply/demand sell modifier. Usage: starecon_s_clear [crop|fish|mineral|all].",
                this.OnStareconSupplyClearModifierCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_s_show",
                "Show the current debug override for the supply/demand sell modifier, if any. Usage: starecon_s_show [crop|fish|mineral|all].",
                this.OnStareconSupplyShowModifierOverrideCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_fm_show",
                "Show fish market simulation state.",
                this.OnStareconFishMarketShowCommand
            );
            helper.ConsoleCommands.Add(
                "starecon_mm_show",
                "Show mineral market simulation state.",
                this.OnStareconMineralMarketShowCommand
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
                _monitor.Log("Usage: starecon_s_dump [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_dump [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            bool wroteAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                wroteAny |= DumpSupplyForCategory(SupplyDebugCategory.Mineral);

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
                _monitor.Log("Usage: starecon_s_mod [crop|fish|mineral] <item id or exact name>", LogLevel.Warn);
                return;
            }

            if (argIndex >= args.Length)
            {
                _monitor.Log("Usage: starecon_s_mod [crop|fish|mineral] <item id or exact name>", LogLevel.Warn);
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
                _monitor.Log("Usage: starecon_s_add [crop|fish|mineral] <item id or exact name> <amount>", LogLevel.Warn);
                return;
            }

            int amountIndex = args.Length - 1;
            if (amountIndex < argIndex)
            {
                _monitor.Log("Usage: starecon_s_add [crop|fish|mineral] <item id or exact name> <amount>", LogLevel.Warn);
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
                _monitor.Log("Usage: starecon_s_add [crop|fish|mineral] <item id or exact name> <amount>", LogLevel.Warn);
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
                _monitor.Log("Usage: starecon_s_reset [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_reset [crop|fish|mineral|all]", LogLevel.Warn);
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

            if (category is not SupplyDebugCategory.Fish and not SupplyDebugCategory.Mineral)
            {
                _monitor.Log("Crop supply does not expose a direct decay command. Use starecon_s_decay [fish|mineral] [days].", LogLevel.Warn);
                return;
            }

            if (args.Length - argIndex > 1)
            {
                _monitor.Log("Usage: starecon_s_decay [fish|mineral] [days]", LogLevel.Warn);
                return;
            }

            int days = 1;
            if (argIndex < args.Length && (!int.TryParse(args[argIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out days) || days <= 0))
            {
                _monitor.Log($"Could not parse '{args[argIndex]}' as a positive day count.", LogLevel.Error);
                return;
            }

            bool changed = category == SupplyDebugCategory.Fish
                ? FishMarketSimulationService.ApplyDebugDailyUpdate(days)
                : MineralMarketSimulationService.ApplyDebugDailyUpdate(days);
            if (!changed)
            {
                _monitor.Log(
                    $"{char.ToUpperInvariant(GetSupplyCategoryLabel(category)[0])}{GetSupplyCategoryLabel(category).Substring(1)} supply decay made no changes. Either no tracked {GetSupplyCategoryPlural(category)} exist yet, the tracked {GetSupplyCategoryPlural(category)} are already neutral, or the save is not ready.",
                    LogLevel.Info
                );
            }
        }

        private void OnStareconSupplySetModifierCommand(string command, string[] args)
        {
            _ = command;

            if (!TryResolveSupplyCategory(args, defaultCategory: SupplyDebugCategory.Crop, out SupplyDebugCategory category, out int argIndex))
            {
                _monitor.Log(
                    $"Usage: starecon_s_set [crop|fish|mineral] <value between {CropSupplyModifierService.MinimumAllowedSellModifier:0.###} and {CropSupplyModifierService.MaximumAllowedSellModifier:0.###}>",
                    LogLevel.Warn
                );
                return;
            }

            if (args.Length - argIndex != 1)
            {
                _monitor.Log(
                    $"Usage: starecon_s_set [crop|fish|mineral] <value between {CropSupplyModifierService.MinimumAllowedSellModifier:0.###} and {CropSupplyModifierService.MaximumAllowedSellModifier:0.###}>",
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
                _monitor.Log("Usage: starecon_s_clear [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_clear [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            bool clearedAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                clearedAny |= ClearSupplyModifierOverride(SupplyDebugCategory.Mineral);

            if (clearedAny)
                return;

            _monitor.Log("No supply/demand modifier override is active for the requested category.", LogLevel.Info);
        }

        private void OnStareconSupplyShowModifierOverrideCommand(string command, string[] args)
        {
            _ = command;

            if (args.Length > 1)
            {
                _monitor.Log("Usage: starecon_s_show [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            if (!TryResolveSupplyScope(args, defaultScope: SupplyDebugScope.Crop, out SupplyDebugScope scope))
            {
                _monitor.Log("Usage: starecon_s_show [crop|fish|mineral|all]", LogLevel.Warn);
                return;
            }

            bool showedAny = false;
            if (scope is SupplyDebugScope.Crop or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Crop);

            if (scope is SupplyDebugScope.Fish or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Fish);

            if (scope is SupplyDebugScope.Mineral or SupplyDebugScope.All)
                showedAny |= ShowSupplyModifierOverride(SupplyDebugCategory.Mineral);

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

            MineralSupplyDataService.ResetTrackedSupply();
        }

        private bool ClearSupplyModifierOverride(SupplyDebugCategory category)
        {
            if (!TryGetSupplyModifierOverride(category, out _))
                return false;

            if (category == SupplyDebugCategory.Crop)
                CropSupplyModifierService.ClearDebugSellModifierOverride();
            else if (category == SupplyDebugCategory.Fish)
                FishSupplyModifierService.ClearDebugSellModifierOverride();
            else
                MineralSupplyModifierService.ClearDebugSellModifierOverride();

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
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static string GetSupplyResolveFailureMessage(SupplyDebugCategory category, string query)
        {
            return category switch
            {
                SupplyDebugCategory.Crop => $"Could not resolve '{query}' to a crop produce item. Use an exact crop name or produce item ID.",
                SupplyDebugCategory.Fish => $"Could not resolve '{query}' to a fish item. Use an exact fish name or fish item ID.",
                SupplyDebugCategory.Mineral => $"Could not resolve '{query}' to a mineral item. Use an exact mineral name or mineral item ID.",
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
                || string.Equals(raw, "minerals", StringComparison.OrdinalIgnoreCase))
            {
                category = SupplyDebugCategory.Mineral;
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
                        : SupplyDebugScope.Mineral;
                return true;
            }

            scope = default;
            return false;
        }

        private static string GetSupplyCategoryLabel(SupplyDebugCategory category)
        {
            return category == SupplyDebugCategory.Crop
                ? "crop"
                : category == SupplyDebugCategory.Fish
                    ? "fish"
                    : "mineral";
        }

        private static string GetSupplyCategoryPlural(SupplyDebugCategory category)
        {
            return category == SupplyDebugCategory.Crop
                ? "crops"
                : category == SupplyDebugCategory.Fish
                    ? "fish"
                    : "minerals";
        }

        private enum SupplyDebugCategory
        {
            Crop,
            Fish,
            Mineral
        }

        private enum SupplyDebugScope
        {
            Crop,
            Fish,
            Mineral,
            All
        }
    }
}
