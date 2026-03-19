using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class PlantExtraMarketSimulationService
    {
        private const string SaveDataKey = "plant-extra-market-simulation";
        private const string LogPrefix = "[PLANT_EXTRA_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new PlantExtraSupplyDataServiceAdapter();
        private static readonly Dictionary<string, PlantExtraMarketDefinition> PlantExtraDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static PlantExtraMarketSimulationSaveData? _activeData;
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
                PlantExtraMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<PlantExtraMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created plant-extra market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded plant-extra market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load plant-extra market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            PlantExtraDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (PlantExtraSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            PlantExtraMarketSimulationSaveData data = EnsureActiveData();
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

            PlantExtraMarketSimulationSaveData data = EnsureActiveData();
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
            PlantExtraDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            PlantExtraMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedPlantExtras = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"PlantExtra market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedPlantExtras.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusPlantExtraItemIds.Count > 0);
            yield return $"PlantExtra market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (PlantExtraMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusPlantExtraItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusPlantExtraItemIds.Select(PlantExtraSupplyTracker.GetPlantExtraDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            PlantExtraMarketSimulationSaveData data,
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
                    $"PlantExtra market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked plant-extra items required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions = BuildPlantExtraDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                string simulatedSeasonKey = GetSeasonKeyForDay(simulatedDay);
                Dictionary<string, float> actorAdjustments = PlantExtraMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    plantExtraDefinitions,
                    updatedScores,
                    simulatedDay,
                    simulatedSeasonKey,
                    traceLines
                );

                foreach (string plantExtraItemId in updatedScores.Keys.ToList())
                {
                    PlantExtraMarketDefinition definition = plantExtraDefinitions[plantExtraItemId];
                    float previousSupply = updatedScores[plantExtraItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float seasonalAdjustment = definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(simulatedSeasonKey)
                        ? GetSeasonalSupplyAdjustment(definition.Temperament)
                        : 0f;
                    float actorAdjustment = actorAdjustments.TryGetValue(plantExtraItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;
                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + seasonalAdjustment + actorAdjustment);
                    updatedScores[plantExtraItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} ({simulatedSeasonKey}) {definition.DisplayName} ({plantExtraItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied plant-extra market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"PlantExtra market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked plant-extra items were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked plant-extra item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static PlantExtraMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static PlantExtraMarketSimulationSaveData CreateNewData()
        {
            return new PlantExtraMarketSimulationSaveData
            {
                LastSimulationDay = GetCurrentDayKey(),
                Actors = PlantExtraMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static PlantExtraMarketSimulationSaveData NormalizeLoadedData(
            PlantExtraMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;
            int normalizedLastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay);
            if (normalizedLastSimulationDay != loadedData.LastSimulationDay)
                shouldPersist = true;

            List<PlantExtraMarketSimulationActorState> normalizedActors = PlantExtraMarketActorSimulationService.NormalizeLoadedActors(
                loadedData.Actors,
                out bool actorsShouldPersist
            );
            shouldPersist |= actorsShouldPersist;

            return new PlantExtraMarketSimulationSaveData
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
                _monitor?.Log($"Failed writing plant-extra market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, PlantExtraMarketDefinition> BuildPlantExtraDefinitions(IEnumerable<string> plantExtraItemIds)
        {
            Dictionary<string, PlantExtraMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string plantExtraItemId in plantExtraItemIds)
            {
                if (!PlantExtraDefinitionsByItemId.TryGetValue(plantExtraItemId, out PlantExtraMarketDefinition? definition))
                {
                    definition = CreatePlantExtraDefinition(plantExtraItemId);
                    PlantExtraDefinitionsByItemId[plantExtraItemId] = definition;
                }

                definitions[plantExtraItemId] = definition;
            }

            return definitions;
        }

        private static PlantExtraMarketDefinition CreatePlantExtraDefinition(string plantExtraItemId)
        {
            string displayName = PlantExtraSupplyTracker.GetPlantExtraDisplayName(plantExtraItemId);
            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
            {
                return new PlantExtraMarketDefinition(
                    plantExtraItemId,
                    displayName,
                    basePrice: 0,
                    PlantExtraEconomicTrait.None,
                    MarketTemperament.Mid,
                    Array.Empty<string>()
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedPlantExtraItemId, out ObjectData? plantExtraData) || plantExtraData is null)
            {
                PlantExtraEconomicTrait fallbackTraits = PlantExtraTraitService.GetTraits(normalizedPlantExtraItemId);
                return new PlantExtraMarketDefinition(
                    normalizedPlantExtraItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(fallbackTraits, 0),
                    PlantExtraTraitService.GetAvailableSeasonKeys(normalizedPlantExtraItemId)
                );
            }

            PlantExtraEconomicTrait traits = PlantExtraTraitService.GetTraits(normalizedPlantExtraItemId);
            return new PlantExtraMarketDefinition(
                normalizedPlantExtraItemId,
                displayName,
                plantExtraData.Price,
                traits,
                DetermineTemperament(traits, plantExtraData.Price),
                PlantExtraTraitService.GetAvailableSeasonKeys(normalizedPlantExtraItemId)
            );
        }

        private static MarketTemperament DetermineTemperament(PlantExtraEconomicTrait traits, int basePrice)
        {
            if (basePrice >= 250
                || ((traits & PlantExtraEconomicTrait.TreeFruit) != 0 && basePrice >= 140)
                || ((traits & PlantExtraEconomicTrait.TreeSapling) != 0 && basePrice >= 200)
                || ((traits & PlantExtraEconomicTrait.Mushroom) != 0 && basePrice >= 150)
                || ((traits & PlantExtraEconomicTrait.TappedProduct) != 0 && basePrice >= 180))
            {
                return MarketTemperament.Luxury;
            }

            if (basePrice <= 60
                || (traits & PlantExtraEconomicTrait.Fertilizer) != PlantExtraEconomicTrait.None
                || (traits & PlantExtraEconomicTrait.FlowerSeedSpecialSeed) != PlantExtraEconomicTrait.None)
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
            float recoveryRate = PlantExtraMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return PlantExtraMarketTuning.BaseSeasonalSupplyStrength * temperament switch
            {
                MarketTemperament.Staple => 1.10f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 0.82f,
                _ => 1f
            };
        }

        private static float ClampSupply(float value)
        {
            return PlantExtraMarketTuning.ClampSupply(value);
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
