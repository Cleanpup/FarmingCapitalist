using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent crop market actor state and daily actor-driven supply pressure.
    /// Kept separate from crop definition building so the simulation flow remains easier to extend.
    /// </summary>
    internal static class CropMarketActorSimulationService
    {
        private const string LogPrefix = "[MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusCropCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("valley-grocer", 0.85f, 0.20f),
            new("seasonal-kitchen", 1.00f, 0.55f),
            new("shipping-broker", 1.10f, -0.35f),
            new("market-speculator", 1.25f, 0.05f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<CropMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            Dictionary<string, float> actorAdjustments = new(KeyComparer);
            for (int i = 0; i < actors.Count; i++)
            {
                ApplyActorActivityForDay(
                    actors[i],
                    actorIndex: i,
                    cropDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<CropMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<CropMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<CropMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, CropMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (CropMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out CropMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<CropMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out CropMarketSimulationActorState? loadedActor))
                {
                    shouldPersist |= ApplyTemplateDefaults(loadedActor, actorTemplate);
                    normalizedActors.Add(loadedActor);
                    continue;
                }

                normalizedActors.Add(CreateActorState(actorTemplate));
                shouldPersist = true;
            }

            if (loadedActorsById.Count != ActorTemplates.Count)
                shouldPersist = true;

            return normalizedActors;
        }

        private static void ApplyActorActivityForDay(
            CropMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, cropDefinitions))
            {
                StartNewTrend(actorState, actorIndex, cropDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusCropProduceItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 37);
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
                if (!cropDefinitions.TryGetValue(cropProduceItemId, out CropMarketDefinition? definition))
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
            CropMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions
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
            CropMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusCropProduceItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (cropDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 11);
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
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions,
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
            CropMarketDefinition definition,
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

        private static string DescribeFocusCrops(
            IEnumerable<string> cropProduceItemIds,
            IReadOnlyDictionary<string, CropMarketDefinition> cropDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                cropProduceItemIds
                    .Where(cropDefinitions.ContainsKey)
                    .Select(cropProduceItemId =>
                    {
                        CropMarketDefinition definition = cropDefinitions[cropProduceItemId];
                        float supplyScore = supplyScores.TryGetValue(cropProduceItemId, out float trackedSupply)
                            ? trackedSupply
                            : CropSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({cropProduceItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(CropMarketSimulationActorState actorState, Random random)
        {
            float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
            return random.NextDouble() <= demandChance;
        }

        private static Random CreateDayRandom(int currentDay, int actorIndex, int salt)
        {
            int seed = unchecked((currentDay * 48611) + (actorIndex * 7919) + salt);
            return new Random(seed);
        }

        private static bool TryNormalizeActorState(
            CropMarketSimulationActorState actorState,
            out CropMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new CropMarketSimulationActorState();
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

        private static bool ApplyTemplateDefaults(CropMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

        private static CropMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new CropMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusCropProduceItemIds = new List<string>()
            };
        }

        private static string FormatSigned(float amount)
        {
            return amount >= 0f
                ? $"+{amount:0.##}"
                : $"{amount:0.##}";
        }

        private readonly record struct ActorTemplate(string ActorId, float InfluenceScale, float DemandBias);
    }
}
