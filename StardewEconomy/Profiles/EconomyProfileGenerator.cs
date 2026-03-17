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
            List<string> cropSellKeys = CropEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (cropSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate crop profile: requires {requiredCount} sell categories but only {cropSellKeys.Count} are registered."
                );
            }

            List<string> fishSellKeys = FishEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fishSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate fish profile: requires {requiredCount} sell categories but only {fishSellKeys.Count} are registered."
                );
            }

            List<string> mineralSellKeys = MineralEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (mineralSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate mineral profile: requires {requiredCount} sell categories but only {mineralSellKeys.Count} are registered."
                );
            }

            SelectCategories(cropSellKeys, seed, out List<string> bonusCategories, out List<string> nerfCategories);
            SelectCategories(fishSellKeys, GetFishGenerationSeed(seed), out List<string> fishBonusCategories, out List<string> fishNerfCategories);
            SelectCategories(
                mineralSellKeys,
                GetMineralGenerationSeed(seed),
                out List<string> mineralBonusCategories,
                out List<string> mineralNerfCategories
            );

            SaveEconomyProfile profile = new()
            {
                ProfileId = string.IsNullOrWhiteSpace(profileId) ? "Randomized" : profileId,
                Seed = seed,
                BonusCategories = bonusCategories,
                NerfCategories = nerfCategories,
                FishBonusCategories = fishBonusCategories,
                FishNerfCategories = fishNerfCategories,
                MineralBonusCategories = mineralBonusCategories,
                MineralNerfCategories = mineralNerfCategories,
                BuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                SellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FishSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                MineralSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
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

            foreach (string category in fishBonusCategories)
                profile.FishSellMultipliers[category] = _settings.BonusSellMultiplier;

            foreach (string category in fishNerfCategories)
                profile.FishSellMultipliers[category] = _settings.NerfSellMultiplier;

            foreach (string category in mineralBonusCategories)
                profile.MineralSellMultipliers[category] = _settings.BonusSellMultiplier;

            foreach (string category in mineralNerfCategories)
                profile.MineralSellMultipliers[category] = _settings.NerfSellMultiplier;

            return profile;
        }

        public void PopulateFishSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> fishSellKeys = FishEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (fishSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate fish profile: requires {requiredCount} sell categories but only {fishSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                fishSellKeys,
                GetFishGenerationSeed(profile.Seed),
                out List<string> fishBonusCategories,
                out List<string> fishNerfCategories
            );

            profile.FishBonusCategories = fishBonusCategories;
            profile.FishNerfCategories = fishNerfCategories;
            profile.FishSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in fishBonusCategories)
                profile.FishSellMultipliers[category] = _settings.BonusSellMultiplier;

            foreach (string category in fishNerfCategories)
                profile.FishSellMultipliers[category] = _settings.NerfSellMultiplier;
        }

        public void PopulateMineralSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> mineralSellKeys = MineralEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (mineralSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate mineral profile: requires {requiredCount} sell categories but only {mineralSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                mineralSellKeys,
                GetMineralGenerationSeed(profile.Seed),
                out List<string> mineralBonusCategories,
                out List<string> mineralNerfCategories
            );

            profile.MineralBonusCategories = mineralBonusCategories;
            profile.MineralNerfCategories = mineralNerfCategories;
            profile.MineralSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in mineralBonusCategories)
                profile.MineralSellMultipliers[category] = _settings.BonusSellMultiplier;

            foreach (string category in mineralNerfCategories)
                profile.MineralSellMultipliers[category] = _settings.NerfSellMultiplier;
        }

        private void SelectCategories(
            List<string> sellKeys,
            int seed,
            out List<string> bonusCategories,
            out List<string> nerfCategories
        )
        {
            List<string> shuffledKeys = new(sellKeys);
            Random rng = new(seed);
            ShuffleInPlace(shuffledKeys, rng);

            bonusCategories = shuffledKeys
                .Take(_settings.BonusCategoryCount)
                .ToList();

            nerfCategories = shuffledKeys
                .Skip(_settings.BonusCategoryCount)
                .Take(_settings.NerfCategoryCount)
                .ToList();
        }

        private static int GetFishGenerationSeed(int seed)
        {
            return unchecked((seed * 397) ^ 0x5F3759DF);
        }

        private static int GetMineralGenerationSeed(int seed)
        {
            return unchecked((seed * 761) ^ 0x1F123BB5);
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
