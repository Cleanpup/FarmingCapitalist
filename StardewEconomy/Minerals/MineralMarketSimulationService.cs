using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class MineralMarketSimulationService
    {
        private const string SaveDataKey = "mineral-market-simulation";
        private const string LogPrefix = "[MINERAL_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new MineralSupplyDataServiceAdapter();
        private static readonly Dictionary<string, MineralMarketDefinition> MineralDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static MineralMarketSimulationSaveData? _activeData;
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
                MineralMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<MineralMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created mineral market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded mineral market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load mineral market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            MineralDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (MineralSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            MineralMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            MineralMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            int startDay = currentDay - elapsedDays + 1;
            return ApplyDailyUpdateRange(data, startDay, currentDay, currentDay, "debug-command");
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            MineralMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedMinerals = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Mineral market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedMinerals={trackedMinerals.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusMineralItemIds.Count > 0);
            yield return $"Mineral market actor trends: active={activeTrends}, configured={data.Actors.Count}.";
        }

        private static bool ApplyDailyUpdateRange(
            MineralMarketSimulationSaveData data,
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
                    $"Mineral market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked minerals required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, MineralMarketDefinition> mineralDefinitions = BuildMineralDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = MineralMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    mineralDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string mineralItemId in updatedScores.Keys.ToList())
                {
                    MineralMarketDefinition definition = mineralDefinitions[mineralItemId];
                    float previousSupply = updatedScores[mineralItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(mineralItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[mineralItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({mineralItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied mineral market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked minerals: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Mineral market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked minerals were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked mineral(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static MineralMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static MineralMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new MineralMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = MineralMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static MineralMarketSimulationSaveData NormalizeLoadedData(
            MineralMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            MineralMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = MineralMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing mineral market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, MineralMarketDefinition> BuildMineralDefinitions(IEnumerable<string> mineralItemIds)
        {
            Dictionary<string, MineralMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string mineralItemId in mineralItemIds)
            {
                if (!MineralDefinitionsByItemId.TryGetValue(mineralItemId, out MineralMarketDefinition? definition))
                {
                    definition = CreateMineralDefinition(mineralItemId);
                    MineralDefinitionsByItemId[mineralItemId] = definition;
                }

                definitions[mineralItemId] = definition;
            }

            return definitions;
        }

        private static MineralMarketDefinition CreateMineralDefinition(string mineralItemId)
        {
            string displayName = MineralSupplyTracker.GetMineralDisplayName(mineralItemId);
            if (!MineralTraitService.TryGetMineralData(mineralItemId, out string normalizedMineralItemId, out ObjectData? mineralData)
                || mineralData is null)
            {
                return new MineralMarketDefinition(
                    mineralItemId,
                    displayName,
                    basePrice: 0,
                    MineralEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            MineralEconomicTrait traits = MineralTraitService.GetTraits(normalizedMineralItemId);
            return new MineralMarketDefinition(
                normalizedMineralItemId,
                displayName,
                mineralData.Price,
                traits,
                DetermineTemperament(traits, mineralData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(MineralEconomicTrait traits, int basePrice)
        {
            if ((traits & MineralEconomicTrait.Luxury) == MineralEconomicTrait.Luxury)
                return MarketTemperament.Luxury;

            if ((traits & MineralEconomicTrait.Rare) == MineralEconomicTrait.Rare && basePrice >= 180)
                return MarketTemperament.Luxury;

            if ((traits & MineralEconomicTrait.Common) == MineralEconomicTrait.Common)
                return MarketTemperament.Staple;

            return MarketTemperament.Mid;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = MineralMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return MineralMarketTuning.ClampSupply(value);
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
