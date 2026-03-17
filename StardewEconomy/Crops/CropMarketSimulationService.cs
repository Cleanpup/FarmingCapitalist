using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;

namespace FarmingCapitalist
{
    internal static class CropMarketSimulationService
    {
        private const string SaveDataKey = "market-simulation";
        private const string LogPrefix = "[MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;
        // A 12.4% daily pull reduces a 200-point surplus to roughly 5 points over 28 days.
        private const float BaseRecoveryRate = 0.124f;
        private const float MinSupplyScore = 20f;
        private const float MaxSupplyScore = 300f;
        private const float BaseSeasonalDemandStrength = 0.75f;

        private static readonly PropertyInfo? CropSeasonsProperty = typeof(CropData).GetProperty("Seasons");
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly HashSet<string> StapleCropNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "parsnip",
            "potato",
            "wheat",
            "corn"
        };
        private static readonly HashSet<string> LuxuryCropNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "starfruit",
            "sweet gem berry",
            "ancient fruit"
        };
        private static readonly Dictionary<string, CropMarketDefinition> CropDefinitionsByProduceId = new(KeyComparer);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CropMarketSimulationSaveData? _activeData;
        private static bool _debugLoggingEnabled;

        public static void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            _helper = helper;
            _monitor = monitor;
            _debugLoggingEnabled = debugLoggingEnabled;
        }

        public static void LoadOrCreateForCurrentSave()
        {
            if (_helper is null)
                return;

            try
            {
                CropMarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<CropMarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created crop market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded crop market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load crop market simulation data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
            CropDefinitionsByProduceId.Clear();
        }

        public static bool RunDailyUpdateIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (CropSupplyModifierService.HasDebugSellModifierOverride)
            {
                LogVerbose("Skipping simulation because the debug sell modifier override is active.");
                return false;
            }

            CropMarketSimulationSaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0 || data.LastSimulationDay >= currentDay)
                return false;

            Dictionary<string, float> updatedScores = new(CropSupplyDataService.GetSnapshot(), KeyComparer);
            if (updatedScores.Count == 0)
            {
                data.LastSimulationDay = currentDay;
                TryWriteActiveData();
                LogVerbose($"No tracked crops were available on day {currentDay}; simulation skipped.");
                return false;
            }

            Dictionary<string, CropMarketDefinition> cropDefinitions = BuildCropDefinitions(updatedScores.Keys);
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;
            Dictionary<string, float> actorAdjustments = CropMarketActorSimulationService.BuildActorAdjustmentsForDay(
                data.Actors,
                cropDefinitions,
                updatedScores,
                currentDay,
                traceLines
            );

            bool changed = false;
            foreach (string cropProduceItemId in updatedScores.Keys.ToList())
            {
                CropMarketDefinition definition = cropDefinitions[cropProduceItemId];
                float previousSupply = updatedScores[cropProduceItemId];
                float meanReversionAdjustment = CalculateMeanReversionAdjustment(previousSupply, definition.Temperament);
                float seasonalAdjustment = definition.GrowsInSeason(Game1.currentSeason)
                    ? -GetSeasonalDemandStrength(definition.Temperament)
                    : 0f;
                float actorAdjustment = actorAdjustments.TryGetValue(cropProduceItemId, out float totalActorAdjustment)
                    ? totalActorAdjustment
                    : 0f;

                float updatedSupply = ClampSupply(previousSupply + meanReversionAdjustment + seasonalAdjustment + actorAdjustment);
                updatedScores[cropProduceItemId] = updatedSupply;

                if (updatedSupply != previousSupply)
                    changed = true;

                traceLines?.Add(
                    $"{LogPrefix} {definition.DisplayName} ({cropProduceItemId}) {previousSupply:0.##} -> {updatedSupply:0.##} "
                    + $"[mean {FormatSigned(meanReversionAdjustment)}, seasonal {FormatSigned(seasonalAdjustment)}, actors {FormatSigned(actorAdjustment)}]"
                );
            }

            if (changed)
                CropSupplyDataService.ReplaceTrackedSupplyScores(updatedScores);

            data.LastSimulationDay = currentDay;
            TryWriteActiveData();

            if (traceLines is not null)
            {
                _monitor?.Log(
                    $"{LogPrefix} Simulated {updatedScores.Count} tracked crop(s) for {Game1.currentSeason} {Game1.dayOfMonth}, Y{Game1.year}.",
                    VerboseLogLevel
                );

                foreach (string line in traceLines)
                    _monitor?.Log(line, VerboseLogLevel);
            }

            return true;
        }

        private static Dictionary<string, CropMarketDefinition> BuildCropDefinitions(IEnumerable<string> cropProduceItemIds)
        {
            Dictionary<string, CropMarketDefinition> definitions = new(KeyComparer);
            foreach (string cropProduceItemId in cropProduceItemIds)
            {
                if (!CropDefinitionsByProduceId.TryGetValue(cropProduceItemId, out CropMarketDefinition? definition))
                {
                    definition = CreateCropDefinition(cropProduceItemId);
                    CropDefinitionsByProduceId[cropProduceItemId] = definition;
                }

                definitions[cropProduceItemId] = definition;
            }

            return definitions;
        }

        private static CropMarketDefinition CreateCropDefinition(string cropProduceItemId)
        {
            string displayName = CropSupplyTracker.GetCropDisplayName(cropProduceItemId);
            if (!CropTraitService.TryGetCropData(cropProduceItemId, out string seedItemId, out CropData? cropData) || cropData is null)
            {
                return new CropMarketDefinition(
                    cropProduceItemId,
                    displayName,
                    seedItemId: string.Empty,
                    MarketTemperament.Mid,
                    Array.Empty<string>()
                );
            }

            return new CropMarketDefinition(
                cropProduceItemId,
                displayName,
                seedItemId,
                DetermineTemperament(displayName, seedItemId),
                ExtractSeasonKeys(cropData)
            );
        }

        private static MarketTemperament DetermineTemperament(string displayName, string seedItemId)
        {
            string normalizedName = displayName.Trim().ToLowerInvariant();
            if (LuxuryCropNames.Contains(normalizedName))
                return MarketTemperament.Luxury;

            if (StapleCropNames.Contains(normalizedName))
                return MarketTemperament.Staple;

            CropEconomicTrait traits = CropTraitService.GetTraits(seedItemId);
            bool expensiveSeed = (traits & CropEconomicTrait.ExpensiveSeed) == CropEconomicTrait.ExpensiveSeed;
            bool cheapSeed = (traits & CropEconomicTrait.CheapSeed) == CropEconomicTrait.CheapSeed;
            bool slowCrop = (traits & CropEconomicTrait.SlowCrop) == CropEconomicTrait.SlowCrop;
            bool fastCrop = (traits & CropEconomicTrait.FastCrop) == CropEconomicTrait.FastCrop;
            bool lowHarvestFrequency = (traits & CropEconomicTrait.LowHarvestFrequency) == CropEconomicTrait.LowHarvestFrequency;
            bool highHarvestFrequency = (traits & CropEconomicTrait.HighHarvestFrequency) == CropEconomicTrait.HighHarvestFrequency;

            if (expensiveSeed && (slowCrop || lowHarvestFrequency))
                return MarketTemperament.Luxury;

            if (cheapSeed && (fastCrop || highHarvestFrequency))
                return MarketTemperament.Staple;

            return MarketTemperament.Mid;
        }

        private static IEnumerable<string> ExtractSeasonKeys(CropData cropData)
        {
            if (CropSeasonsProperty?.GetValue(cropData) is not IEnumerable seasons)
                return Array.Empty<string>();

            List<string> seasonKeys = new();
            foreach (object? seasonValue in seasons)
            {
                string normalizedSeason = NormalizeSeasonKey(seasonValue);
                if (!string.IsNullOrWhiteSpace(normalizedSeason))
                    seasonKeys.Add(normalizedSeason);
            }

            return seasonKeys;
        }

        private static string NormalizeSeasonKey(object? seasonValue)
        {
            if (seasonValue is null)
                return string.Empty;

            if (seasonValue is Season season)
                return Utility.getSeasonKey(season);

            return seasonValue.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static float CalculateMeanReversionAdjustment(float currentSupply, MarketTemperament temperament)
        {
            float recoveryRate = BaseRecoveryRate * GetRecoveryRateMultiplier(temperament);
            return (CropSupplyDataService.NeutralSupplyScore - currentSupply) * recoveryRate;
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

        private static float GetSeasonalDemandStrength(MarketTemperament temperament)
        {
            return BaseSeasonalDemandStrength * temperament switch
            {
                MarketTemperament.Staple => 0.75f,
                MarketTemperament.Mid => 1f,
                MarketTemperament.Luxury => 1.2f,
                _ => 1f
            };
        }

        private static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }

        private static CropMarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CropMarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new CropMarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = CropMarketActorSimulationService.CreateDefaultActorStates()
            };
        }

        private static CropMarketSimulationSaveData NormalizeLoadedData(
            CropMarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            CropMarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = CropMarketActorSimulationService.NormalizeLoadedActors(loadedData.Actors, out bool actorsShouldPersist)
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
                _monitor?.Log($"Failed writing market simulation data: {ex}", LogLevel.Error);
            }
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
