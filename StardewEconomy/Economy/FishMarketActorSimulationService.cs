using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent fish market actor state and daily actor-driven fish supply pressure.
    /// Kept fish-specific so trend tuning can evolve without inheriting crop assumptions.
    /// </summary>
    internal static class FishMarketActorSimulationService
    {
        private const string LogPrefix = "[FISH_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusFishCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("bait-shop wholesaler", 0.85f, -0.15f),
            new("smokehouse vendor", 1.05f, 0.20f),
            new("fish-pond supplier", 0.95f, -0.35f),
            new("traveling fish buyer", 1.20f, 0.35f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<FishMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IList<string>? traceLines
        )
        {
            Dictionary<string, float> actorAdjustments = new(KeyComparer);
            for (int i = 0; i < actors.Count; i++)
            {
                ApplyActorActivityForDay(
                    actors[i],
                    actorIndex: i,
                    fishDefinitions,
                    supplyScores,
                    currentDay,
                    seasonKey,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<FishMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<FishMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<FishMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, FishMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (FishMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out FishMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<FishMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out FishMarketSimulationActorState? loadedActor))
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
            FishMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, fishDefinitions))
            {
                StartNewTrend(actorState, actorIndex, fishDefinitions, supplyScores, currentDay, seasonKey, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusFishItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 41);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusFish(actorState.FocusFishItemIds, fishDefinitions, supplyScores)
                );
            }

            foreach (string fishItemId in actorState.FocusFishItemIds)
            {
                if (!fishDefinitions.TryGetValue(fishItemId, out FishMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(fishItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[fishItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({fishItemId}), "
                    + $"class {definition.Classification}, temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[fishItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            FishMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusFishItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusFishItemIds = new List<string>();
                return false;
            }

            List<string> validFocusFish = actorState.FocusFishItemIds
                .Where(fishItemId => fishDefinitions.ContainsKey(fishItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusFishCount)
                .ToList();

            actorState.FocusFishItemIds = validFocusFish;
            if (validFocusFish.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            FishMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IList<string>? traceLines
        )
        {
            actorState.FocusFishItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (fishDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 13);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusFishCount, fishDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusFishItemIds = PickTrendFocusFish(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                fishDefinitions,
                supplyScores,
                seasonKey
            );

            if (actorState.FocusFishItemIds.Count == 0)
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
                    + DescribeFocusFish(actorState.FocusFishItemIds, fishDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusFish(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            string seasonKey
        )
        {
            List<string> availableFishIds = fishDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableFishIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string fishItemId in availableFishIds)
                    totalWeight += CalculateFocusWeight(fishDefinitions[fishItemId], supplyScores[fishItemId], drivesDemand, seasonKey);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableFishIds.Count - 1;

                for (int i = 0; i < availableFishIds.Count; i++)
                {
                    string candidateFishItemId = availableFishIds[i];
                    running += CalculateFocusWeight(
                        fishDefinitions[candidateFishItemId],
                        supplyScores[candidateFishItemId],
                        drivesDemand,
                        seasonKey
                    );

                    if (roll <= running)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                selections.Add(availableFishIds[selectedIndex]);
                availableFishIds.RemoveAt(selectedIndex);
            }

            return selections;
        }

        private static float CalculateFocusWeight(
            FishMarketDefinition definition,
            float supplyScore,
            bool drivesDemand,
            string seasonKey
        )
        {
            float weight = 1f;

            if (definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(seasonKey))
                weight += 0.35f;

            weight += definition.Classification switch
            {
                FishEconomyClassification.RawFish => 0.18f,
                FishEconomyClassification.SmokedFish => 0.32f,
                FishEconomyClassification.Roe => 0.28f,
                FishEconomyClassification.AgedRoe => 0.36f,
                FishEconomyClassification.SeaweedAlgae => 0.08f,
                _ => 0.18f
            };

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < FishSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > FishSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(0.35f, MathF.Abs(supplyScore - FishSupplyDataService.NeutralSupplyScore) / FishMarketTuning.ActorDeviationWeightRange);
            return weight + deviationWeight;
        }

        private static float GetActorAdjustmentAmount(Random random, MarketTemperament temperament, float influenceScale)
        {
            (float minAmount, float maxAmount) = temperament switch
            {
                MarketTemperament.Staple => (0.08f, 0.20f),
                MarketTemperament.Mid => (0.17f, 0.36f),
                MarketTemperament.Luxury => (0.30f, 0.60f),
                _ => (0.16f, 0.32f)
            };

            float roll = (float)random.NextDouble();
            float baseAmount = minAmount + ((maxAmount - minAmount) * roll);
            return baseAmount * Math.Clamp(influenceScale, 0.50f, 2f);
        }

        private static string DescribeFocusFish(
            IEnumerable<string> fishItemIds,
            IReadOnlyDictionary<string, FishMarketDefinition> fishDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                fishItemIds
                    .Where(fishDefinitions.ContainsKey)
                    .Select(fishItemId =>
                    {
                        FishMarketDefinition definition = fishDefinitions[fishItemId];
                        float supplyScore = supplyScores.TryGetValue(fishItemId, out float trackedSupply)
                            ? trackedSupply
                            : FishSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({fishItemId}, {definition.Classification}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(FishMarketSimulationActorState actorState, Random random)
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
            FishMarketSimulationActorState actorState,
            out FishMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new FishMarketSimulationActorState();
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
            normalizedActorState.FocusFishItemIds = (actorState.FocusFishItemIds ?? new List<string>())
                .Where(fishItemId => FishSupplyTracker.TryNormalizeFishItemId(fishItemId, out _))
                .Select(fishItemId => fishItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? fishItemId.Trim().Substring(3)
                    : fishItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusFishCount)
                .ToList();

            if (normalizedActorState.FocusFishItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(FishMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusFishItemIds.Count > MaxTrendFocusFishCount)
            {
                actorState.FocusFishItemIds = actorState.FocusFishItemIds
                    .Take(MaxTrendFocusFishCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusFishItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static FishMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new FishMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusFishItemIds = new List<string>()
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
