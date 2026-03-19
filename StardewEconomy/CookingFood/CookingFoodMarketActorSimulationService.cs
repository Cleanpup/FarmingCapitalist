using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent cooking-food market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class CookingFoodMarketActorSimulationService
    {
        private const string LogPrefix = "[COOKING_FOOD_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("saloon-chef", 1.00f, 0.15f),
            new("bakery-counter", 0.95f, 0.10f),
            new("adventurer-provisions", 1.10f, 0.30f),
            new("pantry-wholesaler", 0.90f, -0.15f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<CookingFoodMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions,
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
                    cookingFoodDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<CookingFoodMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<CookingFoodMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<CookingFoodMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, CookingFoodMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (CookingFoodMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out CookingFoodMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<CookingFoodMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out CookingFoodMarketSimulationActorState? loadedActor))
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
            CookingFoodMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, cookingFoodDefinitions))
            {
                StartNewTrend(actorState, actorIndex, cookingFoodDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusCookingFoodItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 53);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusCookingFoodItemIds, cookingFoodDefinitions, supplyScores)
                );
            }

            foreach (string cookingFoodItemId in actorState.FocusCookingFoodItemIds)
            {
                if (!cookingFoodDefinitions.TryGetValue(cookingFoodItemId, out CookingFoodMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(cookingFoodItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[cookingFoodItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({cookingFoodItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[cookingFoodItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            CookingFoodMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusCookingFoodItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusCookingFoodItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusCookingFoodItemIds
                .Where(cookingFoodItemId => cookingFoodDefinitions.ContainsKey(cookingFoodItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusCookingFoodItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            CookingFoodMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusCookingFoodItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (cookingFoodDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 23);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, cookingFoodDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusCookingFoodItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                cookingFoodDefinitions,
                supplyScores
            );

            if (actorState.FocusCookingFoodItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusCookingFoodItemIds, cookingFoodDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = cookingFoodDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string cookingFoodItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(cookingFoodDefinitions[cookingFoodItemId], supplyScores[cookingFoodItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateCookingFoodItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        cookingFoodDefinitions[candidateCookingFoodItemId],
                        supplyScores[candidateCookingFoodItemId],
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
            CookingFoodMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += definition.HasTrait(CookingFoodEconomicTrait.Meal) ? 0.22f : 0f;
            weight += definition.HasTrait(CookingFoodEconomicTrait.Dessert) ? 0.18f : 0f;
            weight += definition.HasTrait(CookingFoodEconomicTrait.Drink) ? 0.20f : 0f;
            weight += definition.HasTrait(CookingFoodEconomicTrait.BuffFood) ? 0.30f : 0f;
            weight += definition.HasTrait(CookingFoodEconomicTrait.RecipeOutput) ? 0.12f : 0f;
            weight += definition.HasTrait(CookingFoodEconomicTrait.CookingIngredient) ? 0.08f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < CookingFoodSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > CookingFoodSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - CookingFoodSupplyDataService.NeutralSupplyScore) / CookingFoodMarketTuning.ActorDeviationWeightRange
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
            IEnumerable<string> cookingFoodItemIds,
            IReadOnlyDictionary<string, CookingFoodMarketDefinition> cookingFoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                cookingFoodItemIds
                    .Where(cookingFoodDefinitions.ContainsKey)
                    .Select(cookingFoodItemId =>
                    {
                        CookingFoodMarketDefinition definition = cookingFoodDefinitions[cookingFoodItemId];
                        float supplyScore = supplyScores.TryGetValue(cookingFoodItemId, out float trackedSupply)
                            ? trackedSupply
                            : CookingFoodSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({cookingFoodItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(CookingFoodMarketSimulationActorState actorState, Random random)
        {
            float demandChance = Math.Clamp(0.5f + (actorState.DemandBias * 0.35f), 0.15f, 0.85f);
            return random.NextDouble() <= demandChance;
        }

        private static Random CreateDayRandom(int currentDay, int actorIndex, int salt)
        {
            int seed = unchecked((currentDay * 47653) + (actorIndex * 13217) + salt);
            return new Random(seed);
        }

        private static bool TryNormalizeActorState(
            CookingFoodMarketSimulationActorState actorState,
            out CookingFoodMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new CookingFoodMarketSimulationActorState();
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
            normalizedActorState.FocusCookingFoodItemIds = (actorState.FocusCookingFoodItemIds ?? new List<string>())
                .Where(cookingFoodItemId => CookingFoodSupplyTracker.TryNormalizeCookingFoodItemId(cookingFoodItemId, out _))
                .Select(cookingFoodItemId => cookingFoodItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? cookingFoodItemId.Trim().Substring(3)
                    : cookingFoodItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusCookingFoodItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(CookingFoodMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusCookingFoodItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusCookingFoodItemIds = actorState.FocusCookingFoodItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusCookingFoodItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static CookingFoodMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new CookingFoodMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusCookingFoodItemIds = new List<string>()
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
