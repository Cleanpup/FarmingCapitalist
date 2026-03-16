using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    internal static class FishMarketSimulationService
    {
        private const string SaveDataKey = "fish-market-simulation";
        private const string LogPrefix = "[FISH_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ISupplyDataService SupplyDataService = new FishSupplyDataServiceAdapter();
        private static readonly Dictionary<string, FishMarketDefinition> FishDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static FishMarketSimulationSaveData? _activeData;
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
                FishMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<FishMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created fish market simulation data. LastSimulationDay={_activeData.LastSimulationDay}.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded fish market simulation data. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load fish market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            FishDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (FishSupplyModifierService.HasDebugSellModifierOverride)
                return false;

            FishMarketSimulationSaveData data = EnsureActiveData();
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

            FishMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            int startDay = currentDay - elapsedDays + 1;
            return ApplyDailyUpdateRange(data, startDay, currentDay, currentDay, "debug-command");
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            FishMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedFish = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Fish market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedFish={trackedFish.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusFishItemIds.Count > 0);
            yield return $"Fish market actor trends: active={activeTrends}, configured={data.Actors.Count}.";
        }

        private static bool ApplyDailyUpdateRange(
            FishMarketSimulationSaveData data,
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
                    $"Fish market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked fish required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, FishMarketDefinition> fishDefinitions = BuildFishDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;
            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                string simulatedSeasonKey = GetSeasonKeyForDay(simulatedDay);
                Dictionary<string, float> actorAdjustments = FishMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    fishDefinitions,
                    updatedScores,
                    simulatedDay,
                    simulatedSeasonKey,
                    traceLines
                );

                foreach (string fishItemId in updatedScores.Keys.ToList())
                {
                    FishMarketDefinition definition = fishDefinitions[fishItemId];
                    float previousScore = updatedScores[fishItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousScore, definition.Temperament);
                    float seasonalAdjustment = definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(simulatedSeasonKey)
                        ? GetSeasonalSupplyAdjustment(definition.Temperament, definition.Classification)
                        : 0f;
                    float actorAdjustment = actorAdjustments.TryGetValue(fishItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;
                    float updatedScore = ClampSupply(previousScore + meanReversionAdjustment + seasonalAdjustment + actorAdjustment);
                    updatedScores[fishItemId] = updatedScore;

                    if (updatedScore != previousScore)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} ({simulatedSeasonKey}) {definition.DisplayName} ({fishItemId}) {previousScore:0.##} -> {updatedScore:0.##} [mean {FormatSigned(meanReversionAdjustment)}, seasonal {FormatSigned(seasonalAdjustment)}, actors {FormatSigned(actorAdjustment)}]"
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
                    $"Applied fish market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked fish: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Fish market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked fish were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked fish item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static FishMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static FishMarketSimulationSaveData CreateNewData()
        {
            return new FishMarketSimulationSaveData
            {
                LastSimulationDay = GetCurrentDayKey(),
                Actors = FishMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static FishMarketSimulationSaveData NormalizeLoadedData(
            FishMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;
            int normalizedLastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay);
            if (normalizedLastSimulationDay != loadedData.LastSimulationDay)
                shouldPersist = true;

            List<FishMarketSimulationActorState> normalizedActors = FishMarketActorSimulationService.NormalizeLoadedActors(
                loadedData.Actors,
                out bool actorsShouldPersist
            );
            shouldPersist |= actorsShouldPersist;

            return new FishMarketSimulationSaveData
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
                _monitor?.Log($"Failed writing fish market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static int GetCurrentDayKey()
        {
            return Context.IsWorldReady
                ? Game1.Date.TotalDays
                : -1;
        }

        private static Dictionary<string, FishMarketDefinition> BuildFishDefinitions(IEnumerable<string> fishItemIds)
        {
            Dictionary<string, FishMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string fishItemId in fishItemIds)
            {
                if (!FishDefinitionsByItemId.TryGetValue(fishItemId, out FishMarketDefinition? definition))
                {
                    definition = CreateFishDefinition(fishItemId);
                    FishDefinitionsByItemId[fishItemId] = definition;
                }

                definitions[fishItemId] = definition;
            }

            return definitions;
        }

        private static FishMarketDefinition CreateFishDefinition(string fishItemId)
        {
            if (!FishSupplyTracker.TryGetFishMarketInfo(
                    fishItemId,
                    out string displayName,
                    out FishEconomyClassification classification,
                    out string sourceFishItemId
                ))
            {
                return new FishMarketDefinition(
                    fishItemId,
                    sourceFishItemId: fishItemId,
                    displayName: FishSupplyTracker.GetFishDisplayName(fishItemId),
                    FishEconomyClassification.None,
                    MarketTemperament.Mid,
                    Array.Empty<string>()
                );
            }

            return new FishMarketDefinition(
                fishItemId,
                sourceFishItemId,
                displayName,
                classification,
                DetermineTemperament(classification, sourceFishItemId),
                ExtractSeasonKeys(sourceFishItemId)
            );
        }

        private static MarketTemperament DetermineTemperament(FishEconomyClassification classification, string sourceFishItemId)
        {
            if (classification == FishEconomyClassification.SeaweedAlgae)
                return MarketTemperament.Staple;

            if (classification is FishEconomyClassification.SmokedFish or FishEconomyClassification.Roe or FishEconomyClassification.AgedRoe)
                return MarketTemperament.Luxury;

            if (Context.IsWorldReady && Game1.objectData.TryGetValue(sourceFishItemId, out var data))
            {
                if (data.Price >= 250)
                    return MarketTemperament.Luxury;

                if (data.Price <= 100)
                    return MarketTemperament.Staple;
            }

            return MarketTemperament.Mid;
        }

        private static IEnumerable<string> ExtractSeasonKeys(string sourceFishItemId)
        {
            if (!Context.IsWorldReady)
                return Array.Empty<string>();

            Dictionary<string, string> fishData = DataLoader.Fish(Game1.content);
            if (!fishData.TryGetValue(sourceFishItemId, out string? rawFishData))
                return GetAllSeasonKeys();

            string[] fields = rawFishData.Split('/');
            if (fields.Length <= 6 || string.Equals(fields[1], "trap", StringComparison.OrdinalIgnoreCase))
                return GetAllSeasonKeys();

            string rawSeasonList = fields[6];
            if (string.IsNullOrWhiteSpace(rawSeasonList))
                return GetAllSeasonKeys();

            return rawSeasonList
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(season => season.ToLowerInvariant());
        }

        private static IEnumerable<string> GetAllSeasonKeys()
        {
            return new[] { "spring", "summer", "fall", "winter" };
        }

        private static string GetSeasonKeyForDay(int totalDay)
        {
            WorldDate date = new();
            date.TotalDays = totalDay;
            return date.SeasonKey;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            _ = temperament;

            float updatedSupply = currentSupply + ((SupplyDataService.NeutralSupplyScore - currentSupply) * FishMarketTuning.BaseRecoveryRate);
            if (MathF.Abs(updatedSupply - SupplyDataService.NeutralSupplyScore) <= FishMarketTuning.MeanReversionSnapThreshold)
                updatedSupply = SupplyDataService.NeutralSupplyScore;

            return updatedSupply - currentSupply;
        }

        private static float GetSeasonalSupplyAdjustment(
            MarketTemperament temperament,
            FishEconomyClassification classification
        )
        {
            float classificationMultiplier = classification switch
            {
                FishEconomyClassification.RawFish => 1f,
                FishEconomyClassification.SmokedFish => 0.85f,
                FishEconomyClassification.Roe => 0.75f,
                FishEconomyClassification.AgedRoe => 0.65f,
                FishEconomyClassification.SeaweedAlgae => 0f,
                _ => 1f
            };

            float temperamentMultiplier = temperament switch
            {
                MarketTemperament.Staple => 1.15f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 0.75f,
                _ => 1f
            };

            return FishMarketTuning.BaseSeasonalSupplyStrength * classificationMultiplier * temperamentMultiplier;
        }

        private static float ClampSupply(float value)
        {
            return FishMarketTuning.ClampSupply(value);
        }

        private static string FormatSigned(float amount)
        {
            return amount >= 0f
                ? $"+{amount:0.##}"
                : $"{amount:0.##}";
        }

    }
}
