using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent crafting-extra market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class CraftingExtraMarketActorSimulationService
    {
        private const string LogPrefix = "[CRAFTING_EXTRA_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("builder-yard", 1.10f, 0.15f),
            new("carpenter-crew", 1.05f, 0.20f),
            new("mason-wholesaler", 1.00f, 0.05f),
            new("field-scavenger", 0.95f, -0.05f),
            new("repair-contractor", 1.15f, 0.10f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<CraftingExtraMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions,
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
                    craftingExtraDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<CraftingExtraMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<CraftingExtraMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<CraftingExtraMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, CraftingExtraMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (CraftingExtraMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out CraftingExtraMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<CraftingExtraMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out CraftingExtraMarketSimulationActorState? loadedActor))
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
            CraftingExtraMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, craftingExtraDefinitions))
            {
                StartNewTrend(actorState, actorIndex, craftingExtraDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusCraftingExtraItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 53);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusCraftingExtraItemIds, craftingExtraDefinitions, supplyScores)
                );
            }

            foreach (string craftingExtraItemId in actorState.FocusCraftingExtraItemIds)
            {
                if (!craftingExtraDefinitions.TryGetValue(craftingExtraItemId, out CraftingExtraMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(craftingExtraItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[craftingExtraItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({craftingExtraItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[craftingExtraItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            CraftingExtraMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusCraftingExtraItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusCraftingExtraItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusCraftingExtraItemIds
                .Where(craftingExtraItemId => craftingExtraDefinitions.ContainsKey(craftingExtraItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusCraftingExtraItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            CraftingExtraMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusCraftingExtraItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (craftingExtraDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 23);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, craftingExtraDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusCraftingExtraItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                craftingExtraDefinitions,
                supplyScores
            );

            if (actorState.FocusCraftingExtraItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusCraftingExtraItemIds, craftingExtraDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = craftingExtraDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string craftingExtraItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(craftingExtraDefinitions[craftingExtraItemId], supplyScores[craftingExtraItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateCraftingExtraItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        craftingExtraDefinitions[candidateCraftingExtraItemId],
                        supplyScores[candidateCraftingExtraItemId],
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
            CraftingExtraMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += CraftingExtraEconomyItemRules.IsWood(definition.CraftingExtraItemId) ? 0.30f : 0f;
            weight += CraftingExtraEconomyItemRules.IsStone(definition.CraftingExtraItemId) ? 0.28f : 0f;
            weight += CraftingExtraEconomyItemRules.IsHardwood(definition.CraftingExtraItemId) ? 0.24f : 0f;
            weight += CraftingExtraEconomyItemRules.IsClay(definition.CraftingExtraItemId) ? 0.18f : 0f;
            weight += CraftingExtraEconomyItemRules.IsFiber(definition.CraftingExtraItemId) ? 0.16f : 0f;
            weight += CraftingExtraEconomyItemRules.IsMoss(definition.CraftingExtraItemId) ? 0.15f : 0f;
            weight += CraftingExtraEconomyItemRules.IsSap(definition.CraftingExtraItemId) ? 0.10f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < CraftingExtraSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > CraftingExtraSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - CraftingExtraSupplyDataService.NeutralSupplyScore) / CraftingExtraMarketTuning.ActorDeviationWeightRange
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
            IEnumerable<string> craftingExtraItemIds,
            IReadOnlyDictionary<string, CraftingExtraMarketDefinition> craftingExtraDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                craftingExtraItemIds
                    .Where(craftingExtraDefinitions.ContainsKey)
                    .Select(craftingExtraItemId =>
                    {
                        CraftingExtraMarketDefinition definition = craftingExtraDefinitions[craftingExtraItemId];
                        float supplyScore = supplyScores.TryGetValue(craftingExtraItemId, out float trackedSupply)
                            ? trackedSupply
                            : CraftingExtraSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({craftingExtraItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(CraftingExtraMarketSimulationActorState actorState, Random random)
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
            CraftingExtraMarketSimulationActorState actorState,
            out CraftingExtraMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new CraftingExtraMarketSimulationActorState();
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
            normalizedActorState.FocusCraftingExtraItemIds = (actorState.FocusCraftingExtraItemIds ?? new List<string>())
                .Where(craftingExtraItemId => CraftingExtraSupplyTracker.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out _))
                .Select(craftingExtraItemId => craftingExtraItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? craftingExtraItemId.Trim().Substring(3)
                    : craftingExtraItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusCraftingExtraItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(CraftingExtraMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusCraftingExtraItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusCraftingExtraItemIds = actorState.FocusCraftingExtraItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusCraftingExtraItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static CraftingExtraMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new CraftingExtraMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusCraftingExtraItemIds = new List<string>()
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
