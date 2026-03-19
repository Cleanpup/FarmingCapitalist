using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent plantExtra market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class PlantExtraMarketActorSimulationService
    {
        private const string LogPrefix = "[PLANT_EXTRA_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("orchard-broker", 1.05f, 0.15f),
            new("nursery-supplier", 0.95f, -0.05f),
            new("florist-merchant", 1.00f, 0.25f),
            new("tapper-exporter", 1.15f, 0.10f),
            new("fungi-forager", 1.10f, 0.20f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<PlantExtraMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions,
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
                    plantExtraDefinitions,
                    supplyScores,
                    currentDay,
                    seasonKey,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<PlantExtraMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<PlantExtraMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<PlantExtraMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, PlantExtraMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (PlantExtraMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out PlantExtraMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<PlantExtraMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out PlantExtraMarketSimulationActorState? loadedActor))
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
            PlantExtraMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, plantExtraDefinitions))
            {
                StartNewTrend(actorState, actorIndex, plantExtraDefinitions, supplyScores, currentDay, seasonKey, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusPlantExtraItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 47);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusPlantExtraItemIds, plantExtraDefinitions, supplyScores)
                );
            }

            foreach (string plantExtraItemId in actorState.FocusPlantExtraItemIds)
            {
                if (!plantExtraDefinitions.TryGetValue(plantExtraItemId, out PlantExtraMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(plantExtraItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[plantExtraItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({plantExtraItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[plantExtraItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            PlantExtraMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusPlantExtraItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusPlantExtraItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusPlantExtraItemIds
                .Where(plantExtraItemId => plantExtraDefinitions.ContainsKey(plantExtraItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusPlantExtraItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            PlantExtraMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            string seasonKey,
            IList<string>? traceLines
        )
        {
            actorState.FocusPlantExtraItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (plantExtraDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 19);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, plantExtraDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusPlantExtraItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                plantExtraDefinitions,
                supplyScores,
                seasonKey
            );

            if (actorState.FocusPlantExtraItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusPlantExtraItemIds, plantExtraDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            string seasonKey
        )
        {
            List<string> availableItemIds = plantExtraDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string plantExtraItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(plantExtraDefinitions[plantExtraItemId], supplyScores[plantExtraItemId], drivesDemand, seasonKey);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidatePlantExtraItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        plantExtraDefinitions[candidatePlantExtraItemId],
                        supplyScores[candidatePlantExtraItemId],
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
            PlantExtraMarketDefinition definition,
            float supplyScore,
            bool drivesDemand,
            string seasonKey
        )
        {
            float weight = 1f;

            if (definition.AvailableSeasons.Count < 4 && definition.IsAvailableInSeason(seasonKey))
                weight += 0.35f;

            weight += definition.HasTrait(PlantExtraEconomicTrait.TreeFruit) ? 0.26f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.TreeSapling) ? 0.14f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.Flower) ? 0.18f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.FlowerSeedSpecialSeed) ? 0.12f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.Mushroom) ? 0.20f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.TappedProduct) ? 0.24f : 0f;
            weight += definition.HasTrait(PlantExtraEconomicTrait.Fertilizer) ? 0.10f : 0f;

            if (string.Equals(seasonKey, "winter", StringComparison.OrdinalIgnoreCase)
                && definition.HasTrait(PlantExtraEconomicTrait.Mushroom))
            {
                weight += 0.18f;
            }

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < PlantExtraSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > PlantExtraSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - PlantExtraSupplyDataService.NeutralSupplyScore) / PlantExtraMarketTuning.ActorDeviationWeightRange
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
            IEnumerable<string> plantExtraItemIds,
            IReadOnlyDictionary<string, PlantExtraMarketDefinition> plantExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                plantExtraItemIds
                    .Where(plantExtraDefinitions.ContainsKey)
                    .Select(plantExtraItemId =>
                    {
                        PlantExtraMarketDefinition definition = plantExtraDefinitions[plantExtraItemId];
                        float supplyScore = supplyScores.TryGetValue(plantExtraItemId, out float trackedSupply)
                            ? trackedSupply
                            : PlantExtraSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({plantExtraItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(PlantExtraMarketSimulationActorState actorState, Random random)
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
            PlantExtraMarketSimulationActorState actorState,
            out PlantExtraMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new PlantExtraMarketSimulationActorState();
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
            normalizedActorState.FocusPlantExtraItemIds = (actorState.FocusPlantExtraItemIds ?? new List<string>())
                .Where(plantExtraItemId => PlantExtraSupplyTracker.TryNormalizePlantExtraItemId(plantExtraItemId, out _))
                .Select(plantExtraItemId => plantExtraItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? plantExtraItemId.Trim().Substring(3)
                    : plantExtraItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusPlantExtraItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(PlantExtraMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusPlantExtraItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusPlantExtraItemIds = actorState.FocusPlantExtraItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusPlantExtraItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static PlantExtraMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new PlantExtraMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusPlantExtraItemIds = new List<string>()
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
