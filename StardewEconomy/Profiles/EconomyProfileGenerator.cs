namespace FarmingCapitalist
{
    /// <summary>
    /// Generates static per-save market profiles from a category registry.
    /// </summary>
    internal sealed class EconomyProfileGenerator
    {
        private readonly EconomyProfileGenerationSettings _settings;

        public EconomyProfileGenerator(EconomyProfileGenerationSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public SaveEconomyProfile Generate(string profileId, int seed)
        {
            List<string> sellKeys = CropEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (sellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate profile: requires {requiredCount} sell categories but only {sellKeys.Count} are registered."
                );
            }

            Random rng = new(seed);
            ShuffleInPlace(sellKeys, rng);

            List<string> bonusCategories = sellKeys
                .Take(_settings.BonusCategoryCount)
                .ToList();

            List<string> nerfCategories = sellKeys
                .Skip(_settings.BonusCategoryCount)
                .Take(_settings.NerfCategoryCount)
                .ToList();

            SaveEconomyProfile profile = new()
            {
                ProfileId = string.IsNullOrWhiteSpace(profileId) ? "Randomized" : profileId,
                Seed = seed,
                BonusCategories = bonusCategories,
                NerfCategories = nerfCategories,
                BuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                SellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (string category in bonusCategories)
            {
                profile.SellMultipliers[category] = _settings.BonusSellMultiplier;

                if (_settings.RandomizeBuyMultipliers
                    && CropEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCropEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.BuyMultipliers[category] = _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in nerfCategories)
            {
                profile.SellMultipliers[category] = _settings.NerfSellMultiplier;

                if (_settings.RandomizeBuyMultipliers
                    && CropEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCropEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.BuyMultipliers[category] = _settings.NerfBuyMultiplier;
                }
            }

            return profile;
        }

        private static void ShuffleInPlace(List<string> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
