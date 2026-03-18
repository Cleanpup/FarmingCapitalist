using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class ArtisanGoodMarketSimulationService
    {
        private const string SaveDataKey = "artisan-good-market-simulation";
        private const string LogPrefix = "[ARTISAN_GOOD_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new ArtisanGoodSupplyDataServiceAdapter();
        private static readonly Dictionary<string, ArtisanGoodMarketDefinition> ArtisanGoodDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static ArtisanGoodMarketSimulationSaveData? _activeData;
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
                ArtisanGoodMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<ArtisanGoodMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created artisan good market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded artisan good market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load artisan good market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            ArtisanGoodDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (ArtisanGoodSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            ArtisanGoodMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            ArtisanGoodMarketSimulationSaveData data = EnsureActiveData();
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
            ArtisanGoodDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            ArtisanGoodMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedArtisanGoods = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Artisan good market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedArtisanGoods.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusArtisanGoodItemIds.Count > 0);
            yield return $"Artisan good market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (ArtisanGoodMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusArtisanGoodItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusArtisanGoodItemIds.Select(ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            ArtisanGoodMarketSimulationSaveData data,
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
                    $"Artisan good market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked artisan goods required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions = BuildArtisanGoodDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = ArtisanGoodMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    artisanGoodDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string artisanGoodItemId in updatedScores.Keys.ToList())
                {
                    ArtisanGoodMarketDefinition definition = artisanGoodDefinitions[artisanGoodItemId];
                    float previousSupply = updatedScores[artisanGoodItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(artisanGoodItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[artisanGoodItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({artisanGoodItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied artisan good market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Artisan good market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked artisan goods were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked artisan good item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static ArtisanGoodMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static ArtisanGoodMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new ArtisanGoodMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = ArtisanGoodMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static ArtisanGoodMarketSimulationSaveData NormalizeLoadedData(
            ArtisanGoodMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            ArtisanGoodMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = ArtisanGoodMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing artisan good market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, ArtisanGoodMarketDefinition> BuildArtisanGoodDefinitions(IEnumerable<string> artisanGoodItemIds)
        {
            Dictionary<string, ArtisanGoodMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string artisanGoodItemId in artisanGoodItemIds)
            {
                if (!ArtisanGoodDefinitionsByItemId.TryGetValue(artisanGoodItemId, out ArtisanGoodMarketDefinition? definition))
                {
                    definition = CreateArtisanGoodDefinition(artisanGoodItemId);
                    ArtisanGoodDefinitionsByItemId[artisanGoodItemId] = definition;
                }

                definitions[artisanGoodItemId] = definition;
            }

            return definitions;
        }

        private static ArtisanGoodMarketDefinition CreateArtisanGoodDefinition(string artisanGoodItemId)
        {
            string displayName = ArtisanGoodSupplyTracker.GetArtisanGoodDisplayName(artisanGoodItemId);
            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
            {
                return new ArtisanGoodMarketDefinition(
                    artisanGoodItemId,
                    displayName,
                    basePrice: 0,
                    ArtisanGoodEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedArtisanGoodItemId, out ObjectData? artisanGoodData) || artisanGoodData is null)
            {
                ArtisanGoodEconomicTrait fallbackTraits = ArtisanGoodTraitService.GetTraits(normalizedArtisanGoodItemId);
                return new ArtisanGoodMarketDefinition(
                    normalizedArtisanGoodItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(fallbackTraits, 0)
                );
            }

            ArtisanGoodEconomicTrait traits = ArtisanGoodTraitService.GetTraits(normalizedArtisanGoodItemId);
            return new ArtisanGoodMarketDefinition(
                normalizedArtisanGoodItemId,
                displayName,
                artisanGoodData.Price,
                traits,
                DetermineTemperament(traits, artisanGoodData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(ArtisanGoodEconomicTrait traits, int basePrice)
        {
            if ((traits & ArtisanGoodEconomicTrait.AlcoholBeverage) != 0 && basePrice <= 180)
                return MarketTemperament.Mid;

            if ((traits & (ArtisanGoodEconomicTrait.ClothLoomProduct | ArtisanGoodEconomicTrait.OilProduct)) != 0)
                return MarketTemperament.Luxury;

            if ((traits & ArtisanGoodEconomicTrait.SpecialtyProcessedGood) != 0 && basePrice >= 180)
                return MarketTemperament.Luxury;

            if ((traits & (ArtisanGoodEconomicTrait.Preserve | ArtisanGoodEconomicTrait.DairyArtisanGood)) != 0
                && basePrice <= 250)
            {
                return MarketTemperament.Staple;
            }

            if (basePrice >= 400)
                return MarketTemperament.Luxury;

            return MarketTemperament.Mid;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = ArtisanGoodMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return ArtisanGoodMarketTuning.ClampSupply(value);
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
