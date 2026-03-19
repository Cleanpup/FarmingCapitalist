using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class CraftingExtraMarketSimulationService
    {
        private const string SaveDataKey = "crafting-extra-market-simulation";
        private const string LogPrefix = "[CRAFTING_EXTRA_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new CraftingExtraSupplyDataServiceAdapter();
        private static readonly Dictionary<string, CraftingExtraMarketDefinition> CraftingExtraDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CraftingExtraMarketSimulationSaveData? _activeData;
        private static bool _debugLoggingEnabled;

        public static void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            _helper = helper;
            _monitor = monitor;
            _debugLoggingEnabled = debugLoggingEnabled;
        }

        public static void LoadOrCreateForCurrentSave()
        {
            SupplyDataService.LoadOrCreateForCurrentSave();

            if (_helper is null)
                return;

            try
            {
                CraftingExtraMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<CraftingExtraMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created crafting-extra market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded crafting-extra market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load crafting-extra market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            CraftingExtraDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (CraftingExtraSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            CraftingExtraMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            CraftingExtraMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            int startDay = currentDay - elapsedDays + 1;
            return ApplyDailyUpdateRange(data, startDay, currentDay, currentDay, "debug-command");
        }

        public static void ResetSimulationState()
        {
            _activeData = CreateNewData();
            TryWriteActiveData();
            CraftingExtraDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            CraftingExtraMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedCraftingExtras = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"CraftingExtra market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedCraftingExtras.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusCraftingExtraItemIds.Count > 0);
            yield return $"CraftingExtra market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (CraftingExtraMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusCraftingExtraItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusCraftingExtraItemIds.Select(CraftingExtraSupplyTracker.GetCraftingExtraDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            CraftingExtraMarketSimulationSaveData data,
            int startDay,
            int endDay,
            int finalDay,
            string source
        )
        {
            if (startDay > endDay)
                return false;

            Dictionary<string, float> updatedScores = new(SupplyDataService.GetSnapshot(), StringComparer.OrdinalIgnoreCase);
            if (updatedScores.Count == 0)
            {
                data.LastSimulationDay = finalDay;
                TryWriteActiveData();

                _monitor?.Log(
                    $"CraftingExtra market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked crafting-extra items required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions = BuildCraftingExtraDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = CraftingExtraMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    craftingExtraDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string craftingExtraItemId in updatedScores.Keys.ToList())
                {
                    CraftingExtraMarketDefinition definition = craftingExtraDefinitions[craftingExtraItemId];
                    float previousSupply = updatedScores[craftingExtraItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(craftingExtraItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[craftingExtraItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({craftingExtraItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
                        + $"[mean {FormatSigned(meanReversionAdjustment)}, actors {FormatSigned(actorAdjustment)}]"
                    );
                }
            }

            if (changed)
                SupplyDataService.ReplaceTrackedSupplyScores(updatedScores);

            data.LastSimulationDay = finalDay;
            TryWriteActiveData();

            if (changed)
            {
                _monitor?.Log(
                    $"Applied crafting-extra market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"CraftingExtra market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked crafting-extra items were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked crafting-extra item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static CraftingExtraMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CraftingExtraMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new CraftingExtraMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = CraftingExtraMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static CraftingExtraMarketSimulationSaveData NormalizeLoadedData(
            CraftingExtraMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            CraftingExtraMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = CraftingExtraMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
            };
            shouldPersist |= actorsShouldPersist;

            return normalizedData;
        }

        private static void TryWriteActiveData()
        {
            if (_helper is null || _activeData is null)
                return;

            try
            {
                _helper.Data.WriteSaveData(SaveDataKey, _activeData);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed writing crafting-extra market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, CraftingExtraMarketDefinition> BuildCraftingExtraDefinitions(IEnumerable<string> craftingExtraItemIds)
        {
            Dictionary<string, CraftingExtraMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string craftingExtraItemId in craftingExtraItemIds)
            {
                if (!CraftingExtraDefinitionsByItemId.TryGetValue(craftingExtraItemId, out CraftingExtraMarketDefinition? definition))
                {
                    definition = CreateCraftingExtraDefinition(craftingExtraItemId);
                    CraftingExtraDefinitionsByItemId[craftingExtraItemId] = definition;
                }

                definitions[craftingExtraItemId] = definition;
            }

            return definitions;
        }

        private static CraftingExtraMarketDefinition CreateCraftingExtraDefinition(string craftingExtraItemId)
        {
            string displayName = CraftingExtraSupplyTracker.GetCraftingExtraDisplayName(craftingExtraItemId);
            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
            {
                return new CraftingExtraMarketDefinition(
                    craftingExtraItemId,
                    displayName,
                    basePrice: 0,
                    CraftingExtraEconomicTrait.None,
                    MarketTemperament.Staple
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedCraftingExtraItemId, out ObjectData? craftingExtraData) || craftingExtraData is null)
            {
                CraftingExtraEconomicTrait fallbackTraits = CraftingExtraTraitService.GetTraits(normalizedCraftingExtraItemId);
                return new CraftingExtraMarketDefinition(
                    normalizedCraftingExtraItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(normalizedCraftingExtraItemId, 0)
                );
            }

            CraftingExtraEconomicTrait traits = CraftingExtraTraitService.GetTraits(normalizedCraftingExtraItemId);
            return new CraftingExtraMarketDefinition(
                normalizedCraftingExtraItemId,
                displayName,
                craftingExtraData.Price,
                traits,
                DetermineTemperament(normalizedCraftingExtraItemId, craftingExtraData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(string craftingExtraItemId, int basePrice)
        {
            if (CraftingExtraEconomyItemRules.IsHardwood(craftingExtraItemId)
                || CraftingExtraEconomyItemRules.IsClay(craftingExtraItemId)
                || CraftingExtraEconomyItemRules.IsMoss(craftingExtraItemId))
            {
                return MarketTemperament.Mid;
            }

            if (basePrice >= 25)
                return MarketTemperament.Mid;

            return MarketTemperament.Staple;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = CraftingExtraMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
            return (SupplyDataService.NeutralSupplyScore - currentSupply) * recoveryRate;
        }

        private static float GetRecoveryRateMultiplier(MarketTemperament temperament)
        {
            return temperament switch
            {
                MarketTemperament.Staple => 1.35f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 0.72f,
                _ => 1f
            };
        }

        private static float ClampSupply(float value)
        {
            return CraftingExtraMarketTuning.ClampSupply(value);
        }

        private static int GetCurrentDayKey()
        {
            return Context.IsWorldReady
                ? Game1.Date.TotalDays
                : -1;
        }

        private static string FormatSigned(float amount)
        {
            return amount >= 0f
                ? $"+{amount:0.##}"
                : $"{amount:0.##}";
        }

        private static void LogVerbose(string message)
        {
            if (_debugLoggingEnabled)
                _monitor?.Log($"{LogPrefix} {message}", VerboseLogLevel);
        }
    }
}
