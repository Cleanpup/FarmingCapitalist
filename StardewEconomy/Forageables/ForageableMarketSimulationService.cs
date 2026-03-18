using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class ForageableMarketSimulationService
    {
        private const string SaveDataKey = "forageable-market-simulation";
        private const string LogPrefix = "[FORAGEABLE_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new ForageableSupplyDataServiceAdapter();
        private static readonly Dictionary<string, ForageableMarketDefinition> ForageableDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static ForageableMarketSimulationSaveData? _activeData;
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
                ForageableMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<ForageableMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created forageable market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded forageable market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load forageable market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            ForageableDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (ForageableSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            ForageableMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            if (data.LastSimulationDay < 0)
            {
                data.LastSimulationDay = currentDay;
                TryWriteActiveData();
                return false;
            }

            if (data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, data.LastSimulationDay + 1, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            ForageableMarketSimulationSaveData data = EnsureActiveData();
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
            ForageableDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            ForageableMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedForageables = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Forageable market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedForageables.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusForageableItemIds.Count > 0);
            yield return $"Forageable market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (ForageableMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusForageableItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusForageableItemIds.Select(ForageableSupplyTracker.GetForageableDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            ForageableMarketSimulationSaveData data,
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
                    $"Forageable market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked forageables required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, ForageableMarketDefinition> forageableDefinitions = BuildForageableDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                string simulatedSeasonKey = GetSeasonKeyForDay(simulatedDay);
                Dictionary<string, float> actorAdjustments = ForageableMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    forageableDefinitions,
                    updatedScores,
                    simulatedDay,
                    simulatedSeasonKey,
                    traceLines
                );

                foreach (string forageableItemId in updatedScores.Keys.ToList())
                {
                    ForageableMarketDefinition definition = forageableDefinitions[forageableItemId];
                    float previousSupply = updatedScores[forageableItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float seasonalAdjustment = definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(simulatedSeasonKey)
                        ? GetSeasonalSupplyAdjustment(definition.Temperament)
                        : 0f;
                    float actorAdjustment = actorAdjustments.TryGetValue(forageableItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;
                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + seasonalAdjustment + actorAdjustment);
                    updatedScores[forageableItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} ({simulatedSeasonKey}) {definition.DisplayName} ({forageableItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
                        + $"[mean {FormatSigned(meanReversionAdjustment)}, seasonal {FormatSigned(seasonalAdjustment)}, actors {FormatSigned(actorAdjustment)}]"
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
                    $"Applied forageable market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Forageable market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked forageables were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked forageable item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static ForageableMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static ForageableMarketSimulationSaveData CreateNewData()
        {
            return new ForageableMarketSimulationSaveData
            {
                LastSimulationDay = GetCurrentDayKey(),
                Actors = ForageableMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static ForageableMarketSimulationSaveData NormalizeLoadedData(
            ForageableMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;
            int normalizedLastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay);
            if (normalizedLastSimulationDay != loadedData.LastSimulationDay)
                shouldPersist = true;

            List<ForageableMarketSimulationActorState> normalizedActors = ForageableMarketActorSimulationService.NormalizeLoadedActors(
                loadedData.Actors,
                out bool actorsShouldPersist
            );
            shouldPersist |= actorsShouldPersist;

            return new ForageableMarketSimulationSaveData
            {
                LastSimulationDay = normalizedLastSimulationDay,
                Actors = normalizedActors
            };
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
                _monitor?.Log($"Failed writing forageable market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, ForageableMarketDefinition> BuildForageableDefinitions(IEnumerable<string> forageableItemIds)
        {
            Dictionary<string, ForageableMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string forageableItemId in forageableItemIds)
            {
                if (!ForageableDefinitionsByItemId.TryGetValue(forageableItemId, out ForageableMarketDefinition? definition))
                {
                    definition = CreateForageableDefinition(forageableItemId);
                    ForageableDefinitionsByItemId[forageableItemId] = definition;
                }

                definitions[forageableItemId] = definition;
            }

            return definitions;
        }

        private static ForageableMarketDefinition CreateForageableDefinition(string forageableItemId)
        {
            string displayName = ForageableSupplyTracker.GetForageableDisplayName(forageableItemId);
            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
            {
                return new ForageableMarketDefinition(
                    forageableItemId,
                    displayName,
                    basePrice: 0,
                    ForageableEconomicTrait.None,
                    MarketTemperament.Mid,
                    Array.Empty<string>()
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedForageableItemId, out ObjectData? forageableData) || forageableData is null)
            {
                ForageableEconomicTrait fallbackTraits = ForageableTraitService.GetTraits(normalizedForageableItemId);
                return new ForageableMarketDefinition(
                    normalizedForageableItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(fallbackTraits, 0),
                    ForageableTraitService.GetAvailableSeasonKeys(normalizedForageableItemId)
                );
            }

            ForageableEconomicTrait traits = ForageableTraitService.GetTraits(normalizedForageableItemId);
            return new ForageableMarketDefinition(
                normalizedForageableItemId,
                displayName,
                forageableData.Price,
                traits,
                DetermineTemperament(traits, forageableData.Price),
                ForageableTraitService.GetAvailableSeasonKeys(normalizedForageableItemId)
            );
        }

        private static MarketTemperament DetermineTemperament(ForageableEconomicTrait traits, int basePrice)
        {
            if ((traits & (ForageableEconomicTrait.DesertForage | ForageableEconomicTrait.GingerIslandForage)) != 0
                || basePrice >= 180)
            {
                return MarketTemperament.Luxury;
            }

            if (basePrice <= 50
                || ((traits & ForageableEconomicTrait.GatheredFlowersWildEdibles) != 0 && basePrice <= 80))
            {
                return MarketTemperament.Staple;
            }

            return MarketTemperament.Mid;
        }

        private static string GetSeasonKeyForDay(int totalDay)
        {
            WorldDate date = new();
            date.TotalDays = totalDay;
            return date.SeasonKey;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = ForageableMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
            return (SupplyDataService.NeutralSupplyScore - currentSupply) * recoveryRate;
        }

        private static float GetRecoveryRateMultiplier(MarketTemperament temperament)
        {
            return temperament switch
            {
                MarketTemperament.Staple => 1.30f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 0.78f,
                _ => 1f
            };
        }

        private static float GetSeasonalSupplyAdjustment(MarketTemperament temperament)
        {
            return ForageableMarketTuning.BaseSeasonalSupplyStrength * temperament switch
            {
                MarketTemperament.Staple => 1.10f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 0.82f,
                _ => 1f
            };
        }

        private static float ClampSupply(float value)
        {
            return ForageableMarketTuning.ClampSupply(value);
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
