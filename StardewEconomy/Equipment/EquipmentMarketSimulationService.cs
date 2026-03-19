using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    internal static class EquipmentMarketSimulationService
    {
        private const string SaveDataKey = "equipment-market-simulation";
        private const string LogPrefix = "[EQUIPMENT_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new EquipmentSupplyDataServiceAdapter();
        private static readonly Dictionary<string, EquipmentMarketDefinition> EquipmentDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static EquipmentMarketSimulationSaveData? _activeData;
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
                EquipmentMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<EquipmentMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created equipment market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded equipment market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load equipment market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            EquipmentDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (EquipmentSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            EquipmentMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            EquipmentMarketSimulationSaveData data = EnsureActiveData();
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
            EquipmentDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            EquipmentMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedEquipments = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Equipment market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedEquipments.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusEquipmentItemIds.Count > 0);
            yield return $"Equipment market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (EquipmentMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusEquipmentItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusEquipmentItemIds.Select(EquipmentSupplyTracker.GetEquipmentDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            EquipmentMarketSimulationSaveData data,
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
                    $"Equipment market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked equipment items required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, EquipmentMarketDefinition> equipmentDefinitions = BuildEquipmentDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = EquipmentMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    equipmentDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string equipmentItemId in updatedScores.Keys.ToList())
                {
                    EquipmentMarketDefinition definition = equipmentDefinitions[equipmentItemId];
                    float previousSupply = updatedScores[equipmentItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(equipmentItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[equipmentItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({equipmentItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied equipment market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Equipment market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked equipment items were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked equipment item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static EquipmentMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static EquipmentMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new EquipmentMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = EquipmentMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static EquipmentMarketSimulationSaveData NormalizeLoadedData(
            EquipmentMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            EquipmentMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = EquipmentMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing equipment market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, EquipmentMarketDefinition> BuildEquipmentDefinitions(IEnumerable<string> equipmentItemIds)
        {
            Dictionary<string, EquipmentMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string equipmentItemId in equipmentItemIds)
            {
                if (!EquipmentDefinitionsByItemId.TryGetValue(equipmentItemId, out EquipmentMarketDefinition? definition))
                {
                    definition = CreateEquipmentDefinition(equipmentItemId);
                    EquipmentDefinitionsByItemId[equipmentItemId] = definition;
                }

                definitions[equipmentItemId] = definition;
            }

            return definitions;
        }

        private static EquipmentMarketDefinition CreateEquipmentDefinition(string equipmentItemId)
        {
            string displayName = EquipmentSupplyTracker.GetEquipmentDisplayName(equipmentItemId);
            if (!EquipmentEconomyItemRules.TryCreateEquipmentItem(equipmentItemId, out Item? equipmentItem) || equipmentItem is null)
            {
                return new EquipmentMarketDefinition(
                    equipmentItemId,
                    displayName,
                    basePrice: 0,
                    EquipmentEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            string normalizedEquipmentItemId = equipmentItem.QualifiedItemId;
            EquipmentEconomicTrait traits = EquipmentTraitService.GetTraits(equipmentItem);
            int basePrice = Math.Max(0, equipmentItem.salePrice());
            return new EquipmentMarketDefinition(
                normalizedEquipmentItemId,
                string.IsNullOrWhiteSpace(equipmentItem.DisplayName) ? displayName : equipmentItem.DisplayName,
                basePrice,
                traits,
                DetermineTemperament(traits, basePrice)
            );
        }

        private static MarketTemperament DetermineTemperament(EquipmentEconomicTrait traits, int basePrice)
        {
            if ((traits & EquipmentEconomicTrait.Weapon) != 0)
            {
                if (basePrice >= 3000)
                    return MarketTemperament.Luxury;

                if (basePrice >= 1200)
                    return MarketTemperament.Mid;

                return MarketTemperament.Staple;
            }

            if ((traits & EquipmentEconomicTrait.Ring) != 0)
            {
                if (basePrice >= 2500)
                    return MarketTemperament.Luxury;

                if (basePrice >= 900)
                    return MarketTemperament.Mid;

                return MarketTemperament.Staple;
            }

            if ((traits & EquipmentEconomicTrait.Boots) != 0)
            {
                if (basePrice >= 1800)
                    return MarketTemperament.Luxury;

                if (basePrice >= 700)
                    return MarketTemperament.Mid;

                return MarketTemperament.Staple;
            }

            if ((traits & EquipmentEconomicTrait.WearableEquipment) != 0 && basePrice >= 1200)
                return MarketTemperament.Mid;

            if (basePrice >= 2500)
                return MarketTemperament.Luxury;

            if (basePrice >= 1000)
                return MarketTemperament.Mid;

            return MarketTemperament.Staple;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = EquipmentMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return EquipmentMarketTuning.ClampSupply(value);
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
