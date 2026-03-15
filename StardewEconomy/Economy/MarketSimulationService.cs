using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;

namespace FarmingCapitalist
{
    internal static class MarketSimulationService
    {
        private const string SaveDataKey = "market-simulation";
        private const string LogPrefix = "[MARKET_SIM]";
        private const LogLevel VerboseLogLevel = LogLevel.Trace;
        // A 12.4% daily pull reduces a 200-point surplus to roughly 5 points over 28 days.
        private const float BaseRecoveryRate = 0.124f;
        private const float MinSupplyScore = 20f;
        private const float MaxSupplyScore = 300f;
        private const float BaseSeasonalDemandStrength = 0.75f;
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusCropCount = 3;

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
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("valley-grocer", 0.85f, 0.20f),
            new("seasonal-kitchen", 1.00f, 0.55f),
            new("shipping-broker", 1.10f, -0.35f),
            new("market-speculator", 1.25f, 0.05f)
        };
        private static readonly Dictionary<string, MarketCropDefinition> CropDefinitionsByProduceId = new(KeyComparer);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static MarketSimulationSaveData? _activeData;
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
                MarketSimulationSaveData? loadedData = _helper.Data.ReadSaveData<MarketSimulationSaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        $"Created market simulation data with {_activeData.Actors.Count} persistent actors.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded market simulation data with {_activeData.Actors.Count} persistent actors. LastSimulationDay={_activeData.LastSimulationDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load market simulation data: {ex}", LogLevel.Error);
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

            MarketSimulationSaveData data = EnsureActiveData();
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

            Dictionary<string, MarketCropDefinition> cropDefinitions = BuildCropDefinitions(updatedScores.Keys);
            Dictionary<string, float> actorAdjustments = new(KeyComparer);
            List<string>? traceLines = _debugLoggingEnabled ? new List<string>() : null;

            for (int i = 0; i < data.Actors.Count; i++)
            {
                ApplyActorActivityForDay(
                    data.Actors[i],
                    actorIndex: i,
                    cropDefinitions,
                    updatedScores,
                    actorAdjustments,
                    traceLines
                );
            }

            bool changed = false;
            foreach (string cropProduceItemId in updatedScores.Keys.ToList())
            {
                MarketCropDefinition definition = cropDefinitions[cropProduceItemId];
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

        private static void ApplyActorActivityForDay(
            MarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MarketCropDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, cropDefinitions))
            {
                StartNewTrend(actorState, actorIndex, cropDefinitions, supplyScores, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusCropProduceItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(GetCurrentDayKey(), actorIndex, salt: 37);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusCrops(actorState.FocusCropProduceItemIds, cropDefinitions, supplyScores)
                );
            }

            foreach (string cropProduceItemId in actorState.FocusCropProduceItemIds)
            {
                if (!cropDefinitions.TryGetValue(cropProduceItemId, out MarketCropDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(cropProduceItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[cropProduceItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({cropProduceItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[cropProduceItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
                );
            }

            actorState.TrendDaysRemaining = Math.Max(0, actorState.TrendDaysRemaining - 1);

            if (traceLines is not null && actionSummaries.Count > 0)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} applied {trendType} pressure: {string.Join(", ", actionSummaries)} "
                    + $"({actorState.TrendDaysRemaining} day(s) left in trend)"
                );
            }
        }

        private static bool TryNormalizeExistingTrend(
            MarketSimulationActorState actorState,
            IReadOnlyDictionary<string, MarketCropDefinition> cropDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusCropProduceItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusCropProduceItemIds = new List<string>();
                return false;
            }

            List<string> validFocusCrops = actorState.FocusCropProduceItemIds
                .Where(cropProduceItemId => cropDefinitions.ContainsKey(cropProduceItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusCropCount)
                .ToList();

            actorState.FocusCropProduceItemIds = validFocusCrops;
            if (validFocusCrops.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            MarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MarketCropDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            IList<string>? traceLines
        )
        {
            actorState.FocusCropProduceItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (cropDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(GetCurrentDayKey(), actorIndex, salt: 11);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusCropCount, cropDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusCropProduceItemIds = PickTrendFocusCrops(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                cropDefinitions,
                supplyScores
            );

            if (actorState.FocusCropProduceItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                return;
            }

            if (traceLines is not null)
            {
                string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";
                float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} started a {trendType} trend for {actorState.TrendDaysRemaining} day(s) "
                    + $"(bias {actorState.DemandBias:0.##}, demand chance {demandChance:P0}, influence {actorState.InfluenceScale:0.##}): "
                    + DescribeFocusCrops(actorState.FocusCropProduceItemIds, cropDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusCrops(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, MarketCropDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableCropIds = cropDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableCropIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string cropProduceItemId in availableCropIds)
                    totalWeight += CalculateFocusWeight(cropDefinitions[cropProduceItemId], supplyScores[cropProduceItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableCropIds.Count - 1;

                for (int i = 0; i < availableCropIds.Count; i++)
                {
                    string candidateProduceItemId = availableCropIds[i];
                    running += CalculateFocusWeight(
                        cropDefinitions[candidateProduceItemId],
                        supplyScores[candidateProduceItemId],
                        drivesDemand
                    );

                    if (roll <= running)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                selections.Add(availableCropIds[selectedIndex]);
                availableCropIds.RemoveAt(selectedIndex);
            }

            return selections;
        }

        private static float CalculateFocusWeight(
            MarketCropDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            if (definition.GrowsInSeason(Game1.currentSeason))
                weight += 0.35f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < CropSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > CropSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(0.35f, MathF.Abs(supplyScore - CropSupplyDataService.NeutralSupplyScore) / 300f);
            return weight + deviationWeight;
        }

        private static Dictionary<string, MarketCropDefinition> BuildCropDefinitions(IEnumerable<string> cropProduceItemIds)
        {
            Dictionary<string, MarketCropDefinition> definitions = new(KeyComparer);
            foreach (string cropProduceItemId in cropProduceItemIds)
            {
                if (!CropDefinitionsByProduceId.TryGetValue(cropProduceItemId, out MarketCropDefinition? definition))
                {
                    definition = CreateCropDefinition(cropProduceItemId);
                    CropDefinitionsByProduceId[cropProduceItemId] = definition;
                }

                definitions[cropProduceItemId] = definition;
            }

            return definitions;
        }

        private static MarketCropDefinition CreateCropDefinition(string cropProduceItemId)
        {
            string displayName = CropSupplyTracker.GetCropDisplayName(cropProduceItemId);
            if (!CropTraitService.TryGetCropData(cropProduceItemId, out string seedItemId, out CropData? cropData) || cropData is null)
            {
                return new MarketCropDefinition(
                    cropProduceItemId,
                    displayName,
                    seedItemId: string.Empty,
                    MarketTemperament.Mid,
                    Array.Empty<string>()
                );
            }

            return new MarketCropDefinition(
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

        private static float GetActorAdjustmentAmount(Random random, MarketTemperament temperament, float influenceScale)
        {
            (float minAmount, float maxAmount) = temperament switch
            {
                MarketTemperament.Staple => (0.35f, 0.95f),
                MarketTemperament.Mid => (0.90f, 1.85f),
                MarketTemperament.Luxury => (1.60f, 3.20f),
                _ => (0.75f, 1.50f)
            };

            float roll = (float)random.NextDouble();
            float baseAmount = minAmount + ((maxAmount - minAmount) * roll);
            return baseAmount * Math.Clamp(influenceScale, 0.50f, 2f);
        }

        private static float ClampSupply(float value)
        {
            return Math.Clamp(value, MinSupplyScore, MaxSupplyScore);
        }

        private static string DescribeFocusCrops(
            IEnumerable<string> cropProduceItemIds,
            IReadOnlyDictionary<string, MarketCropDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                cropProduceItemIds
                    .Where(cropDefinitions.ContainsKey)
                    .Select(cropProduceItemId =>
                    {
                        MarketCropDefinition definition = cropDefinitions[cropProduceItemId];
                        float supplyScore = supplyScores.TryGetValue(cropProduceItemId, out float trackedSupply)
                            ? trackedSupply
                            : CropSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({cropProduceItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(MarketSimulationActorState actorState, Random random)
        {
            float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
            return random.NextDouble() <= demandChance;
        }

        private static Random CreateDayRandom(int currentDay, int actorIndex, int salt)
        {
            int seed = unchecked((currentDay * 48611) + (actorIndex * 7919) + salt);
            return new Random(seed);
        }

        private static MarketSimulationSaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static MarketSimulationSaveData CreateNewData()
        {
            int currentDay = GetCurrentDayKey();
            return new MarketSimulationSaveData
            {
                LastSimulationDay = currentDay >= 0
                    ? currentDay - 1
                    : -1,
                Actors = ActorTemplates.Select(CreateActorState).ToList()
            };
        }

        private static MarketSimulationSaveData NormalizeLoadedData(
            MarketSimulationSaveData loadedData,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, MarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedData.Actors is not null)
            {
                foreach (MarketSimulationActorState actorState in loadedData.Actors)
                {
                    if (!TryNormalizeActorState(actorState, out MarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            MarketSimulationSaveData normalizedData = new()
            {
                LastSimulationDay = Math.Max(-1, loadedData.LastSimulationDay),
                Actors = new List<MarketSimulationActorState>()
            };

            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out MarketSimulationActorState? loadedActor))
                {
                    shouldPersist |= ApplyTemplateDefaults(loadedActor, actorTemplate);
                    normalizedData.Actors.Add(loadedActor);
                    continue;
                }

                normalizedData.Actors.Add(CreateActorState(actorTemplate));
                shouldPersist = true;
            }

            if (loadedActorsById.Count != ActorTemplates.Count)
                shouldPersist = true;

            return normalizedData;
        }

        private static bool TryNormalizeActorState(
            MarketSimulationActorState actorState,
            out MarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new MarketSimulationActorState();
            if (actorState is null || string.IsNullOrWhiteSpace(actorState.ActorId))
                return false;

            normalizedActorState.ActorId = actorState.ActorId.Trim();
            normalizedActorState.InfluenceScale = float.IsFinite(actorState.InfluenceScale)
                ? actorState.InfluenceScale
                : 1f;
            normalizedActorState.DemandBias = float.IsFinite(actorState.DemandBias)
                ? Math.Clamp(actorState.DemandBias, -1f, 1f)
                : 0f;
            normalizedActorState.TrendDaysRemaining = Math.Max(0, actorState.TrendDaysRemaining);
            normalizedActorState.TrendDrivesDemand = actorState.TrendDrivesDemand;
            normalizedActorState.FocusCropProduceItemIds = (actorState.FocusCropProduceItemIds ?? new List<string>())
                .Where(cropProduceItemId => CropSupplyTracker.TryNormalizeCropProduceItemId(cropProduceItemId, out _))
                .Select(cropProduceItemId => cropProduceItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? cropProduceItemId.Trim().Substring(3)
                    : cropProduceItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusCropCount)
                .ToList();

            if (normalizedActorState.FocusCropProduceItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(MarketSimulationActorState actorState, ActorTemplate actorTemplate)
        {
            bool changed = false;

            if (!string.Equals(actorState.ActorId, actorTemplate.ActorId, StringComparison.Ordinal))
            {
                actorState.ActorId = actorTemplate.ActorId;
                changed = true;
            }

            float clampedInfluence = Math.Clamp(actorState.InfluenceScale, 0.50f, 2f);
            if (clampedInfluence != actorState.InfluenceScale)
            {
                actorState.InfluenceScale = clampedInfluence;
                changed = true;
            }

            float clampedDemandBias = Math.Clamp(actorState.DemandBias, -1f, 1f);
            if (clampedDemandBias != actorState.DemandBias)
            {
                actorState.DemandBias = clampedDemandBias;
                changed = true;
            }

            if (actorState.FocusCropProduceItemIds.Count > MaxTrendFocusCropCount)
            {
                actorState.FocusCropProduceItemIds = actorState.FocusCropProduceItemIds
                    .Take(MaxTrendFocusCropCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusCropProduceItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static MarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new MarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusCropProduceItemIds = new List<string>()
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

        private readonly record struct ActorTemplate(string ActorId, float InfluenceScale, float DemandBias);
    }
}
