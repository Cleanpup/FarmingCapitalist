using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent forageable market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class ForageableMarketActorSimulationService
    {
        private const string LogPrefix = "[FORAGEABLE_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("forest-forager", 0.90f, -0.10f),
            new("beachcomber-buyer", 1.00f, 0.05f),
            new("wild-edibles-chef", 1.10f, 0.30f),
            new("rare-goods-exporter", 1.20f, 0.20f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<ForageableMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions,
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
                    forageableDefinitions,
                    supplyScores,
                    currentDay,
                    seasonKey,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<ForageableMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<ForageableMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<ForageableMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, ForageableMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (ForageableMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out ForageableMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<ForageableMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out ForageableMarketSimulationActorState? loadedActor))
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
            ForageableMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, forageableDefinitions))
            {
                StartNewTrend(actorState, actorIndex, forageableDefinitions, supplyScores, currentDay, seasonKey, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusForageableItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 47);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusForageableItemIds, forageableDefinitions, supplyScores)
                );
            }

            foreach (string forageableItemId in actorState.FocusForageableItemIds)
            {
                if (!forageableDefinitions.TryGetValue(forageableItemId, out ForageableMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(forageableItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[forageableItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({forageableItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[forageableItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            ForageableMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusForageableItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusForageableItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusForageableItemIds
                .Where(forageableItemId => forageableDefinitions.ContainsKey(forageableItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusForageableItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            ForageableMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IList<string>? traceLines
        )
        {
            actorState.FocusForageableItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (forageableDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 19);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, forageableDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusForageableItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                forageableDefinitions,
                supplyScores,
                seasonKey
            );

            if (actorState.FocusForageableItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusForageableItemIds, forageableDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            string seasonKey
        )
        {
            List<string> availableItemIds = forageableDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string forageableItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(forageableDefinitions[forageableItemId], supplyScores[forageableItemId], drivesDemand, seasonKey);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateForageableItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        forageableDefinitions[candidateForageableItemId],
                        supplyScores[candidateForageableItemId],
                        drivesDemand,
                        seasonKey
                    );

                    if (roll <= running)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                selections.Add(availableItemIds[selectedIndex]);
                availableItemIds.RemoveAt(selectedIndex);
            }

            return selections;
        }

        private static float CalculateFocusWeight(
            ForageableMarketDefinition definition,
            float supplyScore,
            bool drivesDemand,
            string seasonKey
        )
        {
            float weight = 1f;

            if (definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(seasonKey))
                weight += 0.35f;

            weight += definition.HasTrait(ForageableEconomicTrait.SeasonalForage) ? 0.20f : 0f;
            weight += definition.HasTrait(ForageableEconomicTrait.BeachForage) ? 0.18f : 0f;
            weight += definition.HasTrait(ForageableEconomicTrait.ForestForage) ? 0.14f : 0f;
            weight += definition.HasTrait(ForageableEconomicTrait.DesertForage) ? 0.28f : 0f;
            weight += definition.HasTrait(ForageableEconomicTrait.GingerIslandForage) ? 0.30f : 0f;
            weight += definition.HasTrait(ForageableEconomicTrait.GatheredFlowersWildEdibles) ? 0.16f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < ForageableSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > ForageableSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - ForageableSupplyDataService.NeutralSupplyScore) / ForageableMarketTuning.ActorDeviationWeightRange
            );
            return weight + deviationWeight;
        }

        private static float GetActorAdjustmentAmount(Random random, MarketTemperament temperament, float influenceScale)
        {
            (float minAmount, float maxAmount) = temperament switch
            {
                MarketTemperament.Staple => (0.45f, 1.10f),
                MarketTemperament.Mid => (0.95f, 2.00f),
                MarketTemperament.Luxury => (1.45f, 3.10f),
                _ => (0.85f, 1.75f)
            };

            float roll = (float)random.NextDouble();
            float baseAmount = minAmount + ((maxAmount - minAmount) * roll);
            return baseAmount * Math.Clamp(influenceScale, 0.50f, 2f);
        }

        private static string DescribeFocusItems(
            IEnumerable<string> forageableItemIds,
            IReadOnlyDictionary<string, ForageableMarketDefinition> forageableDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                forageableItemIds
                    .Where(forageableDefinitions.ContainsKey)
                    .Select(forageableItemId =>
                    {
                        ForageableMarketDefinition definition = forageableDefinitions[forageableItemId];
                        float supplyScore = supplyScores.TryGetValue(forageableItemId, out float trackedSupply)
                            ? trackedSupply
                            : ForageableSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({forageableItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(ForageableMarketSimulationActorState actorState, Random random)
        {
            float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
            return random.NextDouble() <= demandChance;
        }

        private static Random CreateDayRandom(int currentDay, int actorIndex, int salt)
        {
            int seed = unchecked((currentDay * 53267) + (actorIndex * 12143) + salt);
            return new Random(seed);
        }

        private static bool TryNormalizeActorState(
            ForageableMarketSimulationActorState actorState,
            out ForageableMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new ForageableMarketSimulationActorState();
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
            normalizedActorState.FocusForageableItemIds = (actorState.FocusForageableItemIds ?? new List<string>())
                .Where(forageableItemId => ForageableSupplyTracker.TryNormalizeForageableItemId(forageableItemId, out _))
                .Select(forageableItemId => forageableItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? forageableItemId.Trim().Substring(3)
                    : forageableItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusForageableItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(ForageableMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusForageableItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusForageableItemIds = actorState.FocusForageableItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusForageableItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static ForageableMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new ForageableMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusForageableItemIds = new List<string>()
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
