using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;

namespace FarmingCapitalist
{
    internal static class CookingFoodMarketSimulationService
    {
        private const string SaveDataKey = "cooking-food-market-simulation";
        private const string LogPrefix = "[COOKING_FOOD_MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;

        private static readonly ICategoryDataService SupplyDataService = new CookingFoodSupplyDataServiceAdapter();
        private static readonly Dictionary<string, CookingFoodMarketDefinition> CookingFoodDefinitionsByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CookingFoodMarketSimulationSaveData? _activeData;
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
                CookingFoodMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<CookingFoodMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created cooking food market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded cooking food market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load cooking food market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            CookingFoodDefinitionsByItemId.Clear();
            SupplyDataService.ClearActiveData();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (CookingFoodSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            CookingFoodMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            return ApplyDailyUpdateRange(data, currentDay, currentDay, currentDay, "day-start");
        }

        public static bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            CookingFoodMarketSimulationSaveData data = EnsureActiveData();
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
            CookingFoodDefinitionsByItemId.Clear();
        }

        public static IEnumerable<string> GetDebugStatusLines()
        {
            CookingFoodMarketSimulationSaveData data = EnsureActiveData();
            IReadOnlyDictionary<string, float> trackedCookingFoods = SupplyDataService.GetSnapshot();

            string currentDateLabel = "<save not ready>";
            if (Context.IsWorldReady)
            {
                WorldDate currentDate = new WorldDate(Game1.Date);
                currentDateLabel = $"{currentDate.SeasonKey} {currentDate.DayOfMonth}, Y{currentDate.Year} ({currentDate.TotalDays})";
            }

            yield return $"Cooking food market simulation state: lastSimulationDay={data.LastSimulationDay}, trackedItems={trackedCookingFoods.Count}, actors={data.Actors.Count}, currentDate={currentDateLabel}.";

            int activeTrends = data.Actors.Count(actor => actor.TrendDaysRemaining > 0 && actor.FocusCookingFoodItemIds.Count > 0);
            yield return $"Cooking food market actor trends: active={activeTrends}, configured={data.Actors.Count}.";

            foreach (CookingFoodMarketSimulationActorState actor in data.Actors.OrderBy(actor => actor.ActorId, StringComparer.OrdinalIgnoreCase))
            {
                string trendType = actor.TrendDrivesDemand ? "demand" : "supply";
                string focusSummary = actor.FocusCookingFoodItemIds.Count == 0
                    ? "<none>"
                    : string.Join(", ", actor.FocusCookingFoodItemIds.Select(CookingFoodSupplyTracker.GetCookingFoodDisplayName));

                yield return $"Actor {actor.ActorId}: influence {actor.InfluenceScale:0.##}, bias {actor.DemandBias:0.##}, trend {trendType}, daysRemaining={actor.TrendDaysRemaining}, focus={focusSummary}.";
            }
        }

        private static bool ApplyDailyUpdateRange(
            CookingFoodMarketSimulationSaveData data,
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
                    $"Cooking food market simulation checked {endDay - startDay + 1} day(s) from {source}; no tracked cooking foods required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            Dictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions = BuildCookingFoodDefinitions(updatedScores.Keys);
            bool changed = false;
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int simulatedDay = startDay; simulatedDay <= endDay; simulatedDay++)
            {
                Dictionary<string, float> actorAdjustments = CookingFoodMarketActorSimulationService.BuildActorAdjustmentsForDay(
                    data.Actors,
                    cookingFoodDefinitions,
                    updatedScores,
                    simulatedDay,
                    traceLines
                );

                foreach (string cookingFoodItemId in updatedScores.Keys.ToList())
                {
                    CookingFoodMarketDefinition definition = cookingFoodDefinitions[cookingFoodItemId];
                    float previousSupply = updatedScores[cookingFoodItemId];
                    float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                    float actorAdjustment = actorAdjustments.TryGetValue(cookingFoodItemId, out float totalActorAdjustment)
                        ? totalActorAdjustment
                        : 0f;

                    float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + actorAdjustment);
                    updatedScores[cookingFoodItemId] = updatedSupply;

                    if (updatedSupply != previousSupply)
                        changed = true;

                    traceLines?.Add(
                        $"{LogPrefix} day {simulatedDay} {definition.DisplayName} ({cookingFoodItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
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
                    $"Applied cooking food market mean reversion for {endDay - startDay + 1} day(s) from {source}. Tracked items: {updatedScores.Count}. Neutral baseline remains {SupplyDataService.NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Cooking food market simulation checked {endDay - startDay + 1} day(s) from {source}; tracked cooking foods were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked cooking food item(s) for days {startDay} through {endDay}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return changed;
        }

        private static CookingFoodMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CookingFoodMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new CookingFoodMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = CookingFoodMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static CookingFoodMarketSimulationSaveData NormalizeLoadedData(
            CookingFoodMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            CookingFoodMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = CookingFoodMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing cooking food market simulation data: {ex}", LogLevel.Error);
            }
        }

        private static Dictionary<string, CookingFoodMarketDefinition> BuildCookingFoodDefinitions(IEnumerable<string> cookingFoodItemIds)
        {
            Dictionary<string, CookingFoodMarketDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
            foreach (string cookingFoodItemId in cookingFoodItemIds)
            {
                if (!CookingFoodDefinitionsByItemId.TryGetValue(cookingFoodItemId, out CookingFoodMarketDefinition? definition))
                {
                    definition = CreateCookingFoodDefinition(cookingFoodItemId);
                    CookingFoodDefinitionsByItemId[cookingFoodItemId] = definition;
                }

                definitions[cookingFoodItemId] = definition;
            }

            return definitions;
        }

        private static CookingFoodMarketDefinition CreateCookingFoodDefinition(string cookingFoodItemId)
        {
            string displayName = CookingFoodSupplyTracker.GetCookingFoodDisplayName(cookingFoodItemId);
            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
            {
                return new CookingFoodMarketDefinition(
                    cookingFoodItemId,
                    displayName,
                    basePrice: 0,
                    CookingFoodEconomicTrait.None,
                    MarketTemperament.Mid
                );
            }

            if (!Context.IsWorldReady || !Game1.objectData.TryGetValue(normalizedCookingFoodItemId, out ObjectData? cookingFoodData) || cookingFoodData is null)
            {
                CookingFoodEconomicTrait fallbackTraits = CookingFoodTraitService.GetTraits(normalizedCookingFoodItemId);
                return new CookingFoodMarketDefinition(
                    normalizedCookingFoodItemId,
                    displayName,
                    basePrice: 0,
                    fallbackTraits,
                    DetermineTemperament(fallbackTraits, 0)
                );
            }

            CookingFoodEconomicTrait traits = CookingFoodTraitService.GetTraits(normalizedCookingFoodItemId);
            return new CookingFoodMarketDefinition(
                normalizedCookingFoodItemId,
                displayName,
                cookingFoodData.Price,
                traits,
                DetermineTemperament(traits, cookingFoodData.Price)
            );
        }

        private static MarketTemperament DetermineTemperament(CookingFoodEconomicTrait traits, int basePrice)
        {
            if ((traits & CookingFoodEconomicTrait.CookingIngredient) != 0)
                return MarketTemperament.Staple;

            if ((traits & CookingFoodEconomicTrait.BuffFood) != 0 && basePrice >= 220)
                return MarketTemperament.Luxury;

            if ((traits & CookingFoodEconomicTrait.Drink) != 0)
                return basePrice <= 180
                    ? MarketTemperament.Staple
                    : MarketTemperament.Mid;

            if ((traits & CookingFoodEconomicTrait.Meal) != 0 && basePrice <= 160)
                return MarketTemperament.Staple;

            if ((traits & CookingFoodEconomicTrait.Dessert) != 0 && basePrice >= 300)
                return MarketTemperament.Luxury;

            if ((traits & CookingFoodEconomicTrait.RecipeOutput) != 0 && basePrice <= 120)
                return MarketTemperament.Staple;

            if (basePrice >= 400)
                return MarketTemperament.Luxury;

            return MarketTemperament.Mid;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = CookingFoodMarketTuning.BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
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
            return CookingFoodMarketTuning.ClampSupply(value);
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
