using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class MonsterLootMarketSimulationService
    {
        private const string SaveDataKey = "monster-loot-market-simulation";
        private const string LogPrefix = "[MONSTER_LOOT_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new MonsterLootSupplyDataServiceAdapter();
        private static readonly Dictionary<string, MonsterLootMarketDefinition> MonsterLootDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static MonsterLootMarketSimulationSaveData? _activeData;
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
                MonsterLootMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<MonsterLootMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created monster loot market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded monster loot market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load monster loot market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            MonsterLootDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (MonsterLootSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            MonsterLootMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            MonsterLootMarketSimulationSaveData data = EnsureActiveData();
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
            MonsterLootDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            MonsterLootMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedMonsterLoots = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Monster loot market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedMonsterLoots.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusMonsterLootItemIds.Count > 0);
            yield return $"Monster loot market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (MonsterLootMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusMonsterLootItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusMonsterLootItemIds.Select(MonsterLootSupplyTracker.GetMonsterLootDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            MonsterLootMarketSimulationSaveData data,
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
                    $"Monster loot market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked monster loots required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions = BuildMonsterLootDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = MonsterLootMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    monsterLootDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string monsterLootItemId in updatedScores.Keys.ToList())
                {
                    MonsterLootMarketDefinition definition = monsterLootDefinitions[monsterLootItemId];
                    float previousSupply = updatedScores[monsterLootItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(monsterLootItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[monsterLootItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({monsterLootItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied monster loot market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Monster loot market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked monster loots were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked monster loot item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static MonsterLootMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static MonsterLootMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new MonsterLootMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = MonsterLootMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static MonsterLootMarketSimulationSaveData NormalizeLoadedData(
            MonsterLootMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            MonsterLootMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = MonsterLootMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing monster loot market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, MonsterLootMarketDefinition> BuildMonsterLootDefinitions(IEnumerable<string> monsterLootItemIds)
        {
            Dictionary<string, MonsterLootMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string monsterLootItemId in monsterLootItemIds)
            {
                if (!MonsterLootDefinitionsByItemId.TryGetValue(monsterLootItemId, out MonsterLootMarketDefinition? definition))
                {
                    definition = CreateMonsterLootDefinition(monsterLootItemId);
                    MonsterLootDefinitionsByItemId[monsterLootItemId] = definition;
                }

                definitions[monsterLootItemId] = definition;
            }

            return definitions;
        }

        private static MonsterLootMarketDefinition CreateMonsterLootDefinition(string monsterLootItemId)
        {
            string displayName = MonsterLootSupplyTracker.GetMonsterLootDisplayName(monsterLootItemId);
            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
            {
                return new MonsterLootMarketDefinition(
                    monsterLootItemId,
                    displayName,
                    basePrice: 0,
                    MonsterLootEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedMonsterLootItemId, out ObjectData? monsterLootData) || monsterLootData is null)
            {
                MonsterLootEconomicTrait fallbackTraits = MonsterLootTraitService.GetTraits(normalizedMonsterLootItemId);
                return new MonsterLootMarketDefinition(
                    normalizedMonsterLootItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(fallbackTraits, 0)
                );
            }

            MonsterLootEconomicTrait traits = MonsterLootTraitService.GetTraits(normalizedMonsterLootItemId);
            return new MonsterLootMarketDefinition(
                normalizedMonsterLootItemId,
                displayName,
                monsterLootData.Price,
                traits,
                DetermineTemperament(traits, monsterLootData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(MonsterLootEconomicTrait traits, int basePrice)
        {
            if ((traits & MonsterLootEconomicTrait.SlimeRelatedItem) != 0 && basePrice >= 1000)
                return MarketTemperament.Luxury;

            if ((traits & MonsterLootEconomicTrait.EssenceMagicalDrop) != 0)
                return basePrice >= 45
                    ? MarketTemperament.Mid
                    : MarketTemperament.Staple;

            if ((traits & MonsterLootEconomicTrait.SlimeRelatedItem) != 0)
                return basePrice >= 120
                    ? MarketTemperament.Mid
                    : MarketTemperament.Staple;

            if ((traits & MonsterLootEconomicTrait.BasicMonsterDrop) != 0 && basePrice >= 15)
                return MarketTemperament.Mid;

            if (basePrice >= 1000)
                return MarketTemperament.Luxury;

            return MarketTemperament.Staple;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = MonsterLootMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return MonsterLootMarketTuning.ClampSupply(value);
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
