using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent mineral market actor state and daily actor-driven supply pressure.
    /// Kept separate from mineral definition building so the simulation flow remains easier to extend.
    /// </summary>
    internal static class MineralMarketActorSimulationService
    {
        private const string LogPrefix = "[MINERAL_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusMineralCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("blacksmith-wholesaler", 0.85f, 0.20f),
            new("museum-curator", 1.00f, 0.55f),
            new("collector-broker", 1.10f, -0.35f),
            new("private-appraiser", 1.25f, 0.05f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<MineralMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions,
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
                    mineralDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<MineralMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<MineralMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<MineralMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, MineralMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (MineralMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out MineralMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<MineralMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out MineralMarketSimulationActorState? loadedActor))
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
            MineralMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, mineralDefinitions))
            {
                StartNewTrend(actorState, actorIndex, mineralDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusMineralItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 37);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusMinerals(actorState.FocusMineralItemIds, mineralDefinitions, supplyScores)
                );
            }

            foreach (string mineralItemId in actorState.FocusMineralItemIds)
            {
                if (!mineralDefinitions.TryGetValue(mineralItemId, out MineralMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(mineralItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[mineralItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({mineralItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[mineralItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            MineralMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusMineralItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusMineralItemIds = new List<string>();
                return false;
            }

            List<string> validFocusMinerals = actorState.FocusMineralItemIds
                .Where(mineralItemId => mineralDefinitions.ContainsKey(mineralItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusMineralCount)
                .ToList();

            actorState.FocusMineralItemIds = validFocusMinerals;
            if (validFocusMinerals.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            MineralMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusMineralItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (mineralDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 11);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusMineralCount, mineralDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusMineralItemIds = PickTrendFocusMinerals(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                mineralDefinitions,
                supplyScores
            );

            if (actorState.FocusMineralItemIds.Count == 0)
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
                    + DescribeFocusMinerals(actorState.FocusMineralItemIds, mineralDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusMinerals(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableMineralIds = mineralDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableMineralIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string mineralItemId in availableMineralIds)
                    totalWeight += CalculateFocusWeight(mineralDefinitions[mineralItemId], supplyScores[mineralItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableMineralIds.Count - 1;

                for (int i = 0; i < availableMineralIds.Count; i++)
                {
                    string candidateMineralItemId = availableMineralIds[i];
                    running += CalculateFocusWeight(
                        mineralDefinitions[candidateMineralItemId],
                        supplyScores[candidateMineralItemId],
                        drivesDemand
                    );

                    if (roll <= running)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                selections.Add(availableMineralIds[selectedIndex]);
                availableMineralIds.RemoveAt(selectedIndex);
            }

            return selections;
        }

        private static float CalculateFocusWeight(
            MineralMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            if (definition.IsAvailableInSeason(Game1.currentSeason))
                weight += 0.35f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < MineralSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > MineralSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(0.35f, MathF.Abs(supplyScore - MineralSupplyDataService.NeutralSupplyScore) / MineralMarketTuning.ActorDeviationWeightRange);
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

        private static string DescribeFocusMinerals(
            IEnumerable<string> mineralItemIds,
            IReadOnlyDictionary<string, MineralMarketDefinition> mineralDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                mineralItemIds
                    .Where(mineralDefinitions.ContainsKey)
                    .Select(mineralItemId =>
                    {
                        MineralMarketDefinition definition = mineralDefinitions[mineralItemId];
                        float supplyScore = supplyScores.TryGetValue(mineralItemId, out float trackedSupply)
                            ? trackedSupply
                            : MineralSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({mineralItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(MineralMarketSimulationActorState actorState, Random random)
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
            MineralMarketSimulationActorState actorState,
            out MineralMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new MineralMarketSimulationActorState();
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
            normalizedActorState.FocusMineralItemIds = (actorState.FocusMineralItemIds ?? new List<string>())
                .Where(mineralItemId => MineralSupplyTracker.TryNormalizeMineralItemId(mineralItemId, out _))
                .Select(mineralItemId => mineralItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? mineralItemId.Trim().Substring(3)
                    : mineralItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusMineralCount)
                .ToList();

            if (normalizedActorState.FocusMineralItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(MineralMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusMineralItemIds.Count > MaxTrendFocusMineralCount)
            {
                actorState.FocusMineralItemIds = actorState.FocusMineralItemIds
                    .Take(MaxTrendFocusMineralCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusMineralItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static MineralMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new MineralMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusMineralItemIds = new List<string>()
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
