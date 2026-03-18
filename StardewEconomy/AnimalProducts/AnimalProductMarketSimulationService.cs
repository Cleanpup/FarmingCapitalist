using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class AnimalProductMarketSimulationService
    {
        private const string SaveDataKey = "animal-product-market-simulation";
        private const string LogPrefix = "[ANIMAL_PRODUCT_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new AnimalProductSupplyDataServiceAdapter();
        private static readonly Dictionary<string, AnimalProductMarketDefinition> AnimalProductDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static AnimalProductMarketSimulationSaveData? _activeData;
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
                AnimalProductMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<AnimalProductMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created animal product market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded animal product market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load animal product market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            AnimalProductDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (AnimalProductSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            AnimalProductMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            AnimalProductMarketSimulationSaveData data = EnsureActiveData();
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
            AnimalProductDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            AnimalProductMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedAnimalProducts = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Animal product market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedAnimalProducts.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusAnimalProductItemIds.Count > 0);
            yield return $"Animal product market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (AnimalProductMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusAnimalProductItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusAnimalProductItemIds.Select(AnimalProductSupplyTracker.GetAnimalProductDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            AnimalProductMarketSimulationSaveData data,
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
                    $"Animal product market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked animal products required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, AnimalProductMarketDefinition> animalProductDefinitions = BuildAnimalProductDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = AnimalProductMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    animalProductDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string animalProductItemId in updatedScores.Keys.ToList())
                {
                    AnimalProductMarketDefinition definition = animalProductDefinitions[animalProductItemId];
                    float previousSupply = updatedScores[animalProductItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(animalProductItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[animalProductItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({animalProductItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied animal product market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Animal product market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked animal products were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked animal product item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static AnimalProductMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static AnimalProductMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new AnimalProductMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = AnimalProductMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static AnimalProductMarketSimulationSaveData NormalizeLoadedData(
            AnimalProductMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            AnimalProductMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = AnimalProductMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing animal product market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, AnimalProductMarketDefinition> BuildAnimalProductDefinitions(IEnumerable<string> animalProductItemIds)
        {
            Dictionary<string, AnimalProductMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string animalProductItemId in animalProductItemIds)
            {
                if (!AnimalProductDefinitionsByItemId.TryGetValue(animalProductItemId, out AnimalProductMarketDefinition? definition))
                {
                    definition = CreateAnimalProductDefinition(animalProductItemId);
                    AnimalProductDefinitionsByItemId[animalProductItemId] = definition;
                }

                definitions[animalProductItemId] = definition;
            }

            return definitions;
        }

        private static AnimalProductMarketDefinition CreateAnimalProductDefinition(string animalProductItemId)
        {
            string displayName = AnimalProductSupplyTracker.GetAnimalProductDisplayName(animalProductItemId);
            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
            {
                return new AnimalProductMarketDefinition(
                    animalProductItemId,
                    displayName,
                    basePrice: 0,
                    AnimalProductEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedAnimalProductItemId, out ObjectData? animalProductData) || animalProductData is null)
            {
                return new AnimalProductMarketDefinition(
                    normalizedAnimalProductItemId,
                    displayName,
                    basePrice: 0,
                    AnimalProductTraitService.GetTraits(normalizedAnimalProductItemId),
                    MarketTemperament.Mid
                );
            }

            AnimalProductEconomicTrait traits = AnimalProductTraitService.GetTraits(normalizedAnimalProductItemId);
            return new AnimalProductMarketDefinition(
                normalizedAnimalProductItemId,
                displayName,
                animalProductData.Price,
                traits,
                DetermineTemperament(traits, animalProductData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(AnimalProductEconomicTrait traits, int basePrice)
        {
            if ((traits & (AnimalProductEconomicTrait.Egg | AnimalProductEconomicTrait.Milk)) != 0
                && (traits & AnimalProductEconomicTrait.SpecialtyAnimalGood) == 0)
            {
                return MarketTemperament.Staple;
            }

            if ((traits & AnimalProductEconomicTrait.SpecialtyAnimalGood) == AnimalProductEconomicTrait.SpecialtyAnimalGood || basePrice >= 300)
                return MarketTemperament.Luxury;

            return MarketTemperament.Mid;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = AnimalProductMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return AnimalProductMarketTuning.ClampSupply(value);
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
