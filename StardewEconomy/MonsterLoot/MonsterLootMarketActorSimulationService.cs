using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent monster-loot market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class MonsterLootMarketActorSimulationService
    {
        private const string LogPrefix = "[MONSTER_LOOT_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("adventurer-guild-buyer", 1.05f, 0.15f),
            new("wizard-researcher", 1.10f, 0.30f),
            new("slime-hutch-rancher", 1.15f, 0.10f),
            new("shadow-broker", 1.20f, 0.05f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<MonsterLootMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions,
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
                    monsterLootDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<MonsterLootMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<MonsterLootMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<MonsterLootMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, MonsterLootMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (MonsterLootMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out MonsterLootMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<MonsterLootMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out MonsterLootMarketSimulationActorState? loadedActor))
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
            MonsterLootMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, monsterLootDefinitions))
            {
                StartNewTrend(actorState, actorIndex, monsterLootDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusMonsterLootItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 53);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusMonsterLootItemIds, monsterLootDefinitions, supplyScores)
                );
            }

            foreach (string monsterLootItemId in actorState.FocusMonsterLootItemIds)
            {
                if (!monsterLootDefinitions.TryGetValue(monsterLootItemId, out MonsterLootMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(monsterLootItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[monsterLootItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({monsterLootItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[monsterLootItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            MonsterLootMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusMonsterLootItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusMonsterLootItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusMonsterLootItemIds
                .Where(monsterLootItemId => monsterLootDefinitions.ContainsKey(monsterLootItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusMonsterLootItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            MonsterLootMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusMonsterLootItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (monsterLootDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 23);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, monsterLootDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusMonsterLootItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                monsterLootDefinitions,
                supplyScores
            );

            if (actorState.FocusMonsterLootItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusMonsterLootItemIds, monsterLootDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = monsterLootDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string monsterLootItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(monsterLootDefinitions[monsterLootItemId], supplyScores[monsterLootItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateMonsterLootItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        monsterLootDefinitions[candidateMonsterLootItemId],
                        supplyScores[candidateMonsterLootItemId],
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
            MonsterLootMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += definition.HasTrait(MonsterLootEconomicTrait.BasicMonsterDrop) ? 0.18f : 0f;
            weight += definition.HasTrait(MonsterLootEconomicTrait.SlimeRelatedItem) ? 0.26f : 0f;
            weight += definition.HasTrait(MonsterLootEconomicTrait.EssenceMagicalDrop) ? 0.30f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < MonsterLootSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > MonsterLootSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - MonsterLootSupplyDataService.NeutralSupplyScore) / MonsterLootMarketTuning.ActorDeviationWeightRange
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
            IEnumerable<string> monsterLootItemIds,
            IReadOnlyDictionary<string, MonsterLootMarketDefinition> monsterLootDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                monsterLootItemIds
                    .Where(monsterLootDefinitions.ContainsKey)
                    .Select(monsterLootItemId =>
                    {
                        MonsterLootMarketDefinition definition = monsterLootDefinitions[monsterLootItemId];
                        float supplyScore = supplyScores.TryGetValue(monsterLootItemId, out float trackedSupply)
                            ? trackedSupply
                            : MonsterLootSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({monsterLootItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(MonsterLootMarketSimulationActorState actorState, Random random)
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
            MonsterLootMarketSimulationActorState actorState,
            out MonsterLootMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new MonsterLootMarketSimulationActorState();
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
            normalizedActorState.FocusMonsterLootItemIds = (actorState.FocusMonsterLootItemIds ?? new List<string>())
                .Where(monsterLootItemId => MonsterLootSupplyTracker.TryNormalizeMonsterLootItemId(monsterLootItemId, out _))
                .Select(monsterLootItemId => monsterLootItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? monsterLootItemId.Trim().Substring(3)
                    : monsterLootItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusMonsterLootItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(MonsterLootMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusMonsterLootItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusMonsterLootItemIds = actorState.FocusMonsterLootItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusMonsterLootItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static MonsterLootMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new MonsterLootMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusMonsterLootItemIds = new List<string>()
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
