using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent equipment market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class EquipmentMarketActorSimulationService
    {
        private const string LogPrefix = "[EQUIPMENT_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("guild-quartermaster", 1.10f, 0.25f),
            new("adventurer-outfitter", 1.00f, 0.10f),
            new("treasure-hunter", 1.15f, 0.20f),
            new("traveling-collector", 0.95f, -0.05f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<EquipmentMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions,
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
                    equipmentDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<EquipmentMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<EquipmentMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<EquipmentMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, EquipmentMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (EquipmentMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out EquipmentMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<EquipmentMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out EquipmentMarketSimulationActorState? loadedActor))
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
            EquipmentMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, equipmentDefinitions))
            {
                StartNewTrend(actorState, actorIndex, equipmentDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusEquipmentItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 53);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusEquipmentItemIds, equipmentDefinitions, supplyScores)
                );
            }

            foreach (string equipmentItemId in actorState.FocusEquipmentItemIds)
            {
                if (!equipmentDefinitions.TryGetValue(equipmentItemId, out EquipmentMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(equipmentItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[equipmentItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({equipmentItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[equipmentItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            EquipmentMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusEquipmentItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusEquipmentItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusEquipmentItemIds
                .Where(equipmentItemId => equipmentDefinitions.ContainsKey(equipmentItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusEquipmentItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            EquipmentMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusEquipmentItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (equipmentDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 23);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, equipmentDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusEquipmentItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                equipmentDefinitions,
                supplyScores
            );

            if (actorState.FocusEquipmentItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusEquipmentItemIds, equipmentDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = equipmentDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string equipmentItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(equipmentDefinitions[equipmentItemId], supplyScores[equipmentItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateEquipmentItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        equipmentDefinitions[candidateEquipmentItemId],
                        supplyScores[candidateEquipmentItemId],
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
            EquipmentMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += definition.HasTrait(EquipmentEconomicTrait.Weapon) ? 0.35f : 0f;
            weight += definition.HasTrait(EquipmentEconomicTrait.Ring) ? 0.28f : 0f;
            weight += definition.HasTrait(EquipmentEconomicTrait.Boots) ? 0.22f : 0f;
            weight += definition.HasTrait(EquipmentEconomicTrait.WearableEquipment) ? 0.16f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < EquipmentSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > EquipmentSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - EquipmentSupplyDataService.NeutralSupplyScore) / EquipmentMarketTuning.ActorDeviationWeightRange
            );
            return weight + deviationWeight;
        }

        private static float GetActorAdjustmentAmount(Random random, MarketTemperament temperament, float influenceScale)
        {
            (float minAmount, float maxAmount) = temperament switch
            {
                MarketTemperament.Staple => (0.40f, 1.00f),
                MarketTemperament.Mid => (0.95f, 1.95f),
                MarketTemperament.Luxury => (1.70f, 3.40f),
                _ => (0.75f, 1.50f)
            };

            float roll = (float)random.NextDouble();
            float baseAmount = minAmount + ((maxAmount - minAmount) * roll);
            return baseAmount * Math.Clamp(influenceScale, 0.50f, 2f);
        }

        private static string DescribeFocusItems(
            IEnumerable<string> equipmentItemIds,
            IReadOnlyDictionary<string, EquipmentMarketDefinition> equipmentDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                equipmentItemIds
                    .Where(equipmentDefinitions.ContainsKey)
                    .Select(equipmentItemId =>
                    {
                        EquipmentMarketDefinition definition = equipmentDefinitions[equipmentItemId];
                        float supplyScore = supplyScores.TryGetValue(equipmentItemId, out float trackedSupply)
                            ? trackedSupply
                            : EquipmentSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({equipmentItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(EquipmentMarketSimulationActorState actorState, Random random)
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
            EquipmentMarketSimulationActorState actorState,
            out EquipmentMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new EquipmentMarketSimulationActorState();
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
            normalizedActorState.FocusEquipmentItemIds = (actorState.FocusEquipmentItemIds ?? new List<string>())
                .Select(equipmentItemId => EquipmentSupplyTracker.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId)
                    ? normalizedEquipmentItemId
                    : null)
                .Where(normalizedEquipmentItemId => !string.IsNullOrWhiteSpace(normalizedEquipmentItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .Cast<string>()
                .ToList();

            if (normalizedActorState.FocusEquipmentItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(EquipmentMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusEquipmentItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusEquipmentItemIds = actorState.FocusEquipmentItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusEquipmentItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static EquipmentMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new EquipmentMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusEquipmentItemIds = new List<string>()
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
