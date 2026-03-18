using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent animal product market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class AnimalProductMarketActorSimulationService
    {
        private const string LogPrefix = "[ANIMAL_PRODUCT_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("ranch-wholesaler", 0.90f, 0.20f),
            new("breakfast-supplier", 1.00f, 0.40f),
            new("textile-buyer", 1.05f, -0.10f),
            new("luxury-goods-broker", 1.20f, 0.05f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<AnimalProductMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions,
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
                    animalProductDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<AnimalProductMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<AnimalProductMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<AnimalProductMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, AnimalProductMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (AnimalProductMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out AnimalProductMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<AnimalProductMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out AnimalProductMarketSimulationActorState? loadedActor))
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
            AnimalProductMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, animalProductDefinitions))
            {
                StartNewTrend(actorState, actorIndex, animalProductDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusAnimalProductItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 43);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusAnimalProductItemIds, animalProductDefinitions, supplyScores)
                );
            }

            foreach (string animalProductItemId in actorState.FocusAnimalProductItemIds)
            {
                if (!animalProductDefinitions.TryGetValue(animalProductItemId, out AnimalProductMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(animalProductItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[animalProductItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({animalProductItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[animalProductItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            AnimalProductMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusAnimalProductItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusAnimalProductItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusAnimalProductItemIds
                .Where(animalProductItemId => animalProductDefinitions.ContainsKey(animalProductItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusAnimalProductItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            AnimalProductMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusAnimalProductItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (animalProductDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 17);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, animalProductDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusAnimalProductItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                animalProductDefinitions,
                supplyScores
            );

            if (actorState.FocusAnimalProductItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusAnimalProductItemIds, animalProductDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = animalProductDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string animalProductItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(animalProductDefinitions[animalProductItemId], supplyScores[animalProductItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateAnimalProductItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        animalProductDefinitions[candidateAnimalProductItemId],
                        supplyScores[candidateAnimalProductItemId],
                        drivesDemand
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
            AnimalProductMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < AnimalProductSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > AnimalProductSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - AnimalProductSupplyDataService.NeutralSupplyScore) / AnimalProductMarketTuning.ActorDeviationWeightRange
            );
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

        private static string DescribeFocusItems(
            IEnumerable<string> animalProductItemIds,
            IReadOnlyDictionary<string, AnimalProductMarketDefinition> animalProductDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                animalProductItemIds
                    .Where(animalProductDefinitions.ContainsKey)
                    .Select(animalProductItemId =>
                    {
                        AnimalProductMarketDefinition definition = animalProductDefinitions[animalProductItemId];
                        float supplyScore = supplyScores.TryGetValue(animalProductItemId, out float trackedSupply)
                            ? trackedSupply
                            : AnimalProductSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({animalProductItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(AnimalProductMarketSimulationActorState actorState, Random random)
        {
            float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
            return random.NextDouble() <= demandChance;
        }

        private static Random CreateDayRandom(int currentDay, int actorIndex, int salt)
        {
            int seed = unchecked((currentDay * 44809) + (actorIndex * 9137) + salt);
            return new Random(seed);
        }

        private static bool TryNormalizeActorState(
            AnimalProductMarketSimulationActorState actorState,
            out AnimalProductMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new AnimalProductMarketSimulationActorState();
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
            normalizedActorState.FocusAnimalProductItemIds = (actorState.FocusAnimalProductItemIds ?? new List<string>())
                .Where(animalProductItemId => AnimalProductSupplyTracker.TryNormalizeAnimalProductItemId(animalProductItemId, out _))
                .Select(animalProductItemId => animalProductItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? animalProductItemId.Trim().Substring(3)
                    : animalProductItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusAnimalProductItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(AnimalProductMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusAnimalProductItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusAnimalProductItemIds = actorState.FocusAnimalProductItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusAnimalProductItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static AnimalProductMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new AnimalProductMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusAnimalProductItemIds = new List<string>()
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
