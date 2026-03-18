using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Encapsulates persistent artisan-good market actor state and daily actor-driven supply pressure.
    /// </summary>
    internal static class ArtisanGoodMarketActorSimulationService
    {
        private const string LogPrefix = "[ARTISAN_GOOD_MARKET_SIM]";
        private const int MinTrendDurationDays = 3;
        private const int MaxTrendDurationDays = 7;
        private const int MaxTrendFocusItemCount = 3;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly IReadOnlyList<ActorTemplate> ActorTemplates = new List<ActorTemplate>
        {
            new("tavern-distributor", 0.95f, 0.30f),
            new("pantry-wholesaler", 1.00f, -0.10f),
            new("dairy-exporter", 1.05f, 0.10f),
            new("luxury-artisan-broker", 1.20f, 0.20f)
        };

        public static Dictionary<string, float> BuildActorAdjustmentsForDay(
            IList<ArtisanGoodMarketSimulationActorState> actors,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions,
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
                    artisanGoodDefinitions,
                    supplyScores,
                    currentDay,
                    actorAdjustments,
                    traceLines
                );
            }

            return actorAdjustments;
        }

        public static List<ArtisanGoodMarketSimulationActorState> CreateDefaultActorStates()
        {
            return ActorTemplates.Select(CreateActorState).ToList();
        }

        public static List<ArtisanGoodMarketSimulationActorState> NormalizeLoadedActors(
            IEnumerable<ArtisanGoodMarketSimulationActorState>? loadedActors,
            out bool shouldPersist
        )
        {
            shouldPersist = false;

            Dictionary<string, ArtisanGoodMarketSimulationActorState> loadedActorsById = new(KeyComparer);
            if (loadedActors is not null)
            {
                foreach (ArtisanGoodMarketSimulationActorState actorState in loadedActors)
                {
                    if (!TryNormalizeActorState(actorState, out ArtisanGoodMarketSimulationActorState normalizedActorState))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!loadedActorsById.TryAdd(normalizedActorState.ActorId, normalizedActorState))
                        shouldPersist = true;
                }
            }

            List<ArtisanGoodMarketSimulationActorState> normalizedActors = new();
            foreach (ActorTemplate actorTemplate in ActorTemplates)
            {
                if (loadedActorsById.TryGetValue(actorTemplate.ActorId, out ArtisanGoodMarketSimulationActorState? loadedActor))
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
            ArtisanGoodMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IDictionary<string, float> actorAdjustments,
            IList<string>? traceLines
        )
        {
            bool startedNewTrend = false;
            if (!TryNormalizeExistingTrend(actorState, artisanGoodDefinitions))
            {
                StartNewTrend(actorState, actorIndex, artisanGoodDefinitions, supplyScores, currentDay, traceLines);
                startedNewTrend = true;
            }

            if (actorState.FocusArtisanGoodItemIds.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 53);
            List<string> actionSummaries = new();
            string trendType = actorState.TrendDrivesDemand ? "demand" : "supply";

            if (traceLines is not null && !startedNewTrend)
            {
                traceLines.Add(
                    $"{LogPrefix} actor {actorState.ActorId} continues {trendType} trend with {actorState.TrendDaysRemaining} day(s) remaining: "
                    + DescribeFocusItems(actorState.FocusArtisanGoodItemIds, artisanGoodDefinitions, supplyScores)
                );
            }

            foreach (string artisanGoodItemId in actorState.FocusArtisanGoodItemIds)
            {
                if (!artisanGoodDefinitions.TryGetValue(artisanGoodItemId, out ArtisanGoodMarketDefinition? definition))
                    continue;

                float amount = GetActorAdjustmentAmount(random, definition.Temperament, actorState.InfluenceScale);
                float signedAdjustment = actorState.TrendDrivesDemand
                    ? -amount
                    : amount;

                actorAdjustments.TryGetValue(artisanGoodItemId, out float existingAdjustment);
                float totalActorAdjustment = existingAdjustment + signedAdjustment;
                actorAdjustments[artisanGoodItemId] = totalActorAdjustment;
                actionSummaries.Add($"{definition.DisplayName} {FormatSigned(signedAdjustment)}");

                traceLines?.Add(
                    $"{LogPrefix} actor {actorState.ActorId} {trendType} -> {definition.DisplayName} ({artisanGoodItemId}), "
                    + $"temperament {definition.Temperament}, delta {FormatSigned(signedAdjustment)}, "
                    + $"base supply {supplyScores[artisanGoodItemId]:0.##}, pending actor total {FormatSigned(totalActorAdjustment)}"
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
            ArtisanGoodMarketSimulationActorState actorState,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions
        )
        {
            if (actorState.TrendDaysRemaining <= 0 || actorState.FocusArtisanGoodItemIds.Count == 0)
            {
                actorState.TrendDaysRemaining = 0;
                actorState.FocusArtisanGoodItemIds = new List<string>();
                return false;
            }

            List<string> validFocusItems = actorState.FocusArtisanGoodItemIds
                .Where(artisanGoodItemId => artisanGoodDefinitions.ContainsKey(artisanGoodItemId))
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            actorState.FocusArtisanGoodItemIds = validFocusItems;
            if (validFocusItems.Count > 0)
                return true;

            actorState.TrendDaysRemaining = 0;
            return false;
        }

        private static void StartNewTrend(
            ArtisanGoodMarketSimulationActorState actorState,
            int actorIndex,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores,
            int currentDay,
            IList<string>? traceLines
        )
        {
            actorState.FocusArtisanGoodItemIds = new List<string>();
            actorState.TrendDaysRemaining = 0;

            if (artisanGoodDefinitions.Count == 0)
                return;

            Random random = CreateDayRandom(currentDay, actorIndex, salt: 23);
            actorState.TrendDrivesDemand = ShouldDriveDemand(actorState, random);
            actorState.TrendDaysRemaining = random.Next(MinTrendDurationDays, MaxTrendDurationDays + 1);

            int maxFocusCount = Math.Min(MaxTrendFocusItemCount, artisanGoodDefinitions.Count);
            int focusCount = random.Next(1, maxFocusCount + 1);
            actorState.FocusArtisanGoodItemIds = PickTrendFocusItems(
                random,
                focusCount,
                actorState.TrendDrivesDemand,
                artisanGoodDefinitions,
                supplyScores
            );

            if (actorState.FocusArtisanGoodItemIds.Count == 0)
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
                    + DescribeFocusItems(actorState.FocusArtisanGoodItemIds, artisanGoodDefinitions, supplyScores)
                );
            }
        }

        private static List<string> PickTrendFocusItems(
            Random random,
            int focusCount,
            bool drivesDemand,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            List<string> availableItemIds = artisanGoodDefinitions.Keys.ToList();
            List<string> selections = new();

            while (availableItemIds.Count > 0 && selections.Count < focusCount)
            {
                float totalWeight = 0f;
                foreach (string artisanGoodItemId in availableItemIds)
                    totalWeight += CalculateFocusWeight(artisanGoodDefinitions[artisanGoodItemId], supplyScores[artisanGoodItemId], drivesDemand);

                float roll = (float)(random.NextDouble() * totalWeight);
                float running = 0f;
                int selectedIndex = availableItemIds.Count - 1;

                for (int i = 0; i < availableItemIds.Count; i++)
                {
                    string candidateArtisanGoodItemId = availableItemIds[i];
                    running += CalculateFocusWeight(
                        artisanGoodDefinitions[candidateArtisanGoodItemId],
                        supplyScores[candidateArtisanGoodItemId],
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
            ArtisanGoodMarketDefinition definition,
            float supplyScore,
            bool drivesDemand
        )
        {
            float weight = 1f;

            weight += definition.HasTrait(ArtisanGoodEconomicTrait.AlcoholBeverage) ? 0.24f : 0f;
            weight += definition.HasTrait(ArtisanGoodEconomicTrait.Preserve) ? 0.14f : 0f;
            weight += definition.HasTrait(ArtisanGoodEconomicTrait.DairyArtisanGood) ? 0.16f : 0f;
            weight += definition.HasTrait(ArtisanGoodEconomicTrait.ClothLoomProduct) ? 0.28f : 0f;
            weight += definition.HasTrait(ArtisanGoodEconomicTrait.OilProduct) ? 0.22f : 0f;
            weight += definition.HasTrait(ArtisanGoodEconomicTrait.SpecialtyProcessedGood) ? 0.30f : 0f;

            weight += definition.Temperament switch
            {
                MarketTemperament.Staple => 0.10f,
                MarketTemperament.Mid => 0.22f,
                MarketTemperament.Luxury => 0.38f,
                _ => 0.20f
            };

            if (drivesDemand && supplyScore < ArtisanGoodSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            if (!drivesDemand && supplyScore > ArtisanGoodSupplyDataService.NeutralSupplyScore)
                weight += 0.25f;

            float deviationWeight = Math.Min(
                0.35f,
                MathF.Abs(supplyScore - ArtisanGoodSupplyDataService.NeutralSupplyScore) / ArtisanGoodMarketTuning.ActorDeviationWeightRange
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
            IEnumerable<string> artisanGoodItemIds,
            IReadOnlyDictionary<string, ArtisanGoodMarketDefinition> artisanGoodDefinitions,
            IReadOnlyDictionary<string, float> supplyScores
        )
        {
            return string.Join(
                ", ",
                artisanGoodItemIds
                    .Where(artisanGoodDefinitions.ContainsKey)
                    .Select(artisanGoodItemId =>
                    {
                        ArtisanGoodMarketDefinition definition = artisanGoodDefinitions[artisanGoodItemId];
                        float supplyScore = supplyScores.TryGetValue(artisanGoodItemId, out float trackedSupply)
                            ? trackedSupply
                            : ArtisanGoodSupplyDataService.NeutralSupplyScore;
                        return $"{definition.DisplayName} ({artisanGoodItemId}, {definition.Temperament}, supply {supplyScore:0.##})";
                    })
            );
        }

        private static bool ShouldDriveDemand(ArtisanGoodMarketSimulationActorState actorState, Random random)
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
            ArtisanGoodMarketSimulationActorState actorState,
            out ArtisanGoodMarketSimulationActorState normalizedActorState
        )
        {
            normalizedActorState = new ArtisanGoodMarketSimulationActorState();
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
            normalizedActorState.FocusArtisanGoodItemIds = (actorState.FocusArtisanGoodItemIds ?? new List<string>())
                .Where(artisanGoodItemId => ArtisanGoodSupplyTracker.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out _))
                .Select(artisanGoodItemId => artisanGoodItemId.Trim().StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                    ? artisanGoodItemId.Trim().Substring(3)
                    : artisanGoodItemId.Trim())
                .Distinct(KeyComparer)
                .Take(MaxTrendFocusItemCount)
                .ToList();

            if (normalizedActorState.FocusArtisanGoodItemIds.Count == 0)
                normalizedActorState.TrendDaysRemaining = 0;

            return true;
        }

        private static bool ApplyTemplateDefaults(ArtisanGoodMarketSimulationActorState actorState, ActorTemplate actorTemplate)
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

            if (actorState.FocusArtisanGoodItemIds.Count > MaxTrendFocusItemCount)
            {
                actorState.FocusArtisanGoodItemIds = actorState.FocusArtisanGoodItemIds
                    .Take(MaxTrendFocusItemCount)
                    .ToList();
                changed = true;
            }

            if (actorState.FocusArtisanGoodItemIds.Count == 0 && actorState.TrendDaysRemaining > 0)
            {
                actorState.TrendDaysRemaining = 0;
                changed = true;
            }

            return changed;
        }

        private static ArtisanGoodMarketSimulationActorState CreateActorState(ActorTemplate actorTemplate)
        {
            return new ArtisanGoodMarketSimulationActorState
            {
                ActorId = actorTemplate.ActorId,
                InfluenceScale = actorTemplate.InfluenceScale,
                DemandBias = actorTemplate.DemandBias,
                TrendDaysRemaining = 0,
                TrendDrivesDemand = true,
                FocusArtisanGoodItemIds = new List<string>()
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
