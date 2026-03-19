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

            List<string> animalProductSellKeys = AnimalProductEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (animalProductSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate animal product profile: requires {requiredCount} sell categories but only {animalProductSellKeys.Count} are registered."
                );
            }

            List<string> forageableSellKeys = ForageableEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (forageableSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate forageable profile: requires {requiredCount} sell categories but only {forageableSellKeys.Count} are registered."
                );
            }

            List<string> plantExtraSellKeys = PlantExtraEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (plantExtraSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate plant-extra profile: requires {requiredCount} sell categories but only {plantExtraSellKeys.Count} are registered."
                );
            }

            List<string> artisanGoodSellKeys = ArtisanGoodEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (artisanGoodSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate artisan good profile: requires {requiredCount} sell categories but only {artisanGoodSellKeys.Count} are registered."
                );
            }

            List<string> cookingFoodSellKeys = CookingFoodEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cookingFoodSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate cooking food profile: requires {requiredCount} sell categories but only {cookingFoodSellKeys.Count} are registered."
                );
            }

            List<string> monsterLootSellKeys = MonsterLootEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            int requiredMonsterLootCount = _settings.MonsterLootBonusCategoryCount + _settings.MonsterLootNerfCategoryCount;
            if (monsterLootSellKeys.Count < requiredMonsterLootCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate monster-loot profile: requires {requiredMonsterLootCount} sell categories but only {monsterLootSellKeys.Count} are registered."
                );
            }

            List<string> equipmentSellKeys = EquipmentEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (equipmentSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate equipment profile: requires {requiredCount} sell categories but only {equipmentSellKeys.Count} are registered."
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
            SelectCategories(
                animalProductSellKeys,
                GetAnimalProductGenerationSeed(seed),
                out List<string> animalProductBonusCategories,
                out List<string> animalProductNerfCategories
            );
            SelectCategories(
                forageableSellKeys,
                GetForageableGenerationSeed(seed),
                out List<string> forageableBonusCategories,
                out List<string> forageableNerfCategories
            );
            SelectCategories(
                plantExtraSellKeys,
                GetPlantExtraGenerationSeed(seed),
                out List<string> plantExtraBonusCategories,
                out List<string> plantExtraNerfCategories
            );
            SelectCategories(
                artisanGoodSellKeys,
                GetArtisanGoodGenerationSeed(seed),
                out List<string> artisanGoodBonusCategories,
                out List<string> artisanGoodNerfCategories
            );
            SelectCategories(
                cookingFoodSellKeys,
                GetCookingFoodGenerationSeed(seed),
                out List<string> cookingFoodBonusCategories,
                out List<string> cookingFoodNerfCategories
            );
            SelectCategories(
                monsterLootSellKeys,
                GetMonsterLootGenerationSeed(seed),
                _settings.MonsterLootBonusCategoryCount,
                _settings.MonsterLootNerfCategoryCount,
                out List<string> monsterLootBonusCategories,
                out List<string> monsterLootNerfCategories
            );
            SelectCategories(
                equipmentSellKeys,
                GetEquipmentGenerationSeed(seed),
                out List<string> equipmentBonusCategories,
                out List<string> equipmentNerfCategories
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
                AnimalProductBonusCategories = animalProductBonusCategories,
                AnimalProductNerfCategories = animalProductNerfCategories,
                ForageableBonusCategories = forageableBonusCategories,
                ForageableNerfCategories = forageableNerfCategories,
                PlantExtraBonusCategories = plantExtraBonusCategories,
                PlantExtraNerfCategories = plantExtraNerfCategories,
                CraftingExtraBonusCategories = new List<string>(),
                CraftingExtraNerfCategories = new List<string>(),
                ArtisanGoodBonusCategories = artisanGoodBonusCategories,
                ArtisanGoodNerfCategories = artisanGoodNerfCategories,
                CookingFoodBonusCategories = cookingFoodBonusCategories,
                CookingFoodNerfCategories = cookingFoodNerfCategories,
                MonsterLootBonusCategories = monsterLootBonusCategories,
                MonsterLootNerfCategories = monsterLootNerfCategories,
                EquipmentBonusCategories = equipmentBonusCategories,
                EquipmentNerfCategories = equipmentNerfCategories,
                BuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                SellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                FishSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                MineralSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                AnimalProductBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                AnimalProductSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                ForageableBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                ForageableSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                PlantExtraBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                PlantExtraSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                CraftingExtraBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                CraftingExtraSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                ArtisanGoodBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                ArtisanGoodSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                CookingFoodBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                CookingFoodSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                MonsterLootBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                MonsterLootSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                EquipmentBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase),
                EquipmentSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
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

            foreach (string category in animalProductBonusCategories)
            {
                profile.AnimalProductSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (AnimalProductEconomyCategoryRegistry.TryGetCategory(category, out RandomizableAnimalProductEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.AnimalProductBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in animalProductNerfCategories)
            {
                profile.AnimalProductSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (AnimalProductEconomyCategoryRegistry.TryGetCategory(category, out RandomizableAnimalProductEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.AnimalProductBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in forageableBonusCategories)
            {
                profile.ForageableSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (ForageableEconomyCategoryRegistry.TryGetCategory(category, out RandomizableForageableEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.ForageableBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in forageableNerfCategories)
            {
                profile.ForageableSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (ForageableEconomyCategoryRegistry.TryGetCategory(category, out RandomizableForageableEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.ForageableBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in plantExtraBonusCategories)
            {
                profile.PlantExtraSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (PlantExtraEconomyCategoryRegistry.TryGetCategory(category, out RandomizablePlantExtraEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.PlantExtraBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in plantExtraNerfCategories)
            {
                profile.PlantExtraSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (PlantExtraEconomyCategoryRegistry.TryGetCategory(category, out RandomizablePlantExtraEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.PlantExtraBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in artisanGoodBonusCategories)
            {
                profile.ArtisanGoodSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (ArtisanGoodEconomyCategoryRegistry.TryGetCategory(category, out RandomizableArtisanGoodEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.ArtisanGoodBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in artisanGoodNerfCategories)
            {
                profile.ArtisanGoodSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (ArtisanGoodEconomyCategoryRegistry.TryGetCategory(category, out RandomizableArtisanGoodEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.ArtisanGoodBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in cookingFoodBonusCategories)
            {
                profile.CookingFoodSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (CookingFoodEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCookingFoodEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.CookingFoodBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in cookingFoodNerfCategories)
            {
                profile.CookingFoodSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (CookingFoodEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCookingFoodEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.CookingFoodBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in monsterLootBonusCategories)
            {
                profile.MonsterLootSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (MonsterLootEconomyCategoryRegistry.TryGetCategory(category, out RandomizableMonsterLootEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.MonsterLootBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in monsterLootNerfCategories)
            {
                profile.MonsterLootSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (MonsterLootEconomyCategoryRegistry.TryGetCategory(category, out RandomizableMonsterLootEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.MonsterLootBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

            foreach (string category in equipmentBonusCategories)
            {
                profile.EquipmentSellMultipliers[category] = _settings.BonusSellMultiplier;

                if (EquipmentEconomyCategoryRegistry.TryGetCategory(category, out RandomizableEquipmentEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.EquipmentBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                        ? _settings.BonusSellMultiplier
                        : _settings.BonusBuyMultiplier;
                }
            }

            foreach (string category in equipmentNerfCategories)
            {
                profile.EquipmentSellMultipliers[category] = _settings.NerfSellMultiplier;

                if (EquipmentEconomyCategoryRegistry.TryGetCategory(category, out RandomizableEquipmentEconomyCategoryDefinition definition)
                    && definition.SupportsBuy)
                {
                    profile.EquipmentBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                        ? _settings.NerfSellMultiplier
                        : _settings.NerfBuyMultiplier;
                }
            }

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

        public void PopulateAnimalProductSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> animalProductSellKeys = AnimalProductEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (animalProductSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate animal product profile: requires {requiredCount} sell categories but only {animalProductSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                animalProductSellKeys,
                GetAnimalProductGenerationSeed(profile.Seed),
                out List<string> animalProductBonusCategories,
                out List<string> animalProductNerfCategories
            );

            profile.AnimalProductBonusCategories = animalProductBonusCategories;
            profile.AnimalProductNerfCategories = animalProductNerfCategories;
            profile.AnimalProductBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.AnimalProductSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in animalProductBonusCategories)
            {
                profile.AnimalProductSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.AnimalProductBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in animalProductNerfCategories)
            {
                profile.AnimalProductSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.AnimalProductBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulateForageableSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> forageableSellKeys = ForageableEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (forageableSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate forageable profile: requires {requiredCount} sell categories but only {forageableSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                forageableSellKeys,
                GetForageableGenerationSeed(profile.Seed),
                out List<string> forageableBonusCategories,
                out List<string> forageableNerfCategories
            );

            profile.ForageableBonusCategories = forageableBonusCategories;
            profile.ForageableNerfCategories = forageableNerfCategories;
            profile.ForageableBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.ForageableSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in forageableBonusCategories)
            {
                profile.ForageableSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.ForageableBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in forageableNerfCategories)
            {
                profile.ForageableSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.ForageableBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulatePlantExtraSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> plantExtraSellKeys = PlantExtraEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (plantExtraSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate plant-extra profile: requires {requiredCount} sell categories but only {plantExtraSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                plantExtraSellKeys,
                GetPlantExtraGenerationSeed(profile.Seed),
                out List<string> plantExtraBonusCategories,
                out List<string> plantExtraNerfCategories
            );

            profile.PlantExtraBonusCategories = plantExtraBonusCategories;
            profile.PlantExtraNerfCategories = plantExtraNerfCategories;
            profile.PlantExtraBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.PlantExtraSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in plantExtraBonusCategories)
            {
                profile.PlantExtraSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.PlantExtraBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in plantExtraNerfCategories)
            {
                profile.PlantExtraSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.PlantExtraBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulateArtisanGoodSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> artisanGoodSellKeys = ArtisanGoodEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (artisanGoodSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate artisan good profile: requires {requiredCount} sell categories but only {artisanGoodSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                artisanGoodSellKeys,
                GetArtisanGoodGenerationSeed(profile.Seed),
                out List<string> artisanGoodBonusCategories,
                out List<string> artisanGoodNerfCategories
            );

            profile.ArtisanGoodBonusCategories = artisanGoodBonusCategories;
            profile.ArtisanGoodNerfCategories = artisanGoodNerfCategories;
            profile.ArtisanGoodBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.ArtisanGoodSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in artisanGoodBonusCategories)
            {
                profile.ArtisanGoodSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.ArtisanGoodBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in artisanGoodNerfCategories)
            {
                profile.ArtisanGoodSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.ArtisanGoodBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulateCookingFoodSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> cookingFoodSellKeys = CookingFoodEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (cookingFoodSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate cooking food profile: requires {requiredCount} sell categories but only {cookingFoodSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                cookingFoodSellKeys,
                GetCookingFoodGenerationSeed(profile.Seed),
                out List<string> cookingFoodBonusCategories,
                out List<string> cookingFoodNerfCategories
            );

            profile.CookingFoodBonusCategories = cookingFoodBonusCategories;
            profile.CookingFoodNerfCategories = cookingFoodNerfCategories;
            profile.CookingFoodBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.CookingFoodSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in cookingFoodBonusCategories)
            {
                profile.CookingFoodSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.CookingFoodBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in cookingFoodNerfCategories)
            {
                profile.CookingFoodSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.CookingFoodBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulateMonsterLootSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> monsterLootSellKeys = MonsterLootEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.MonsterLootBonusCategoryCount + _settings.MonsterLootNerfCategoryCount;
            if (monsterLootSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate monster-loot profile: requires {requiredCount} sell categories but only {monsterLootSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                monsterLootSellKeys,
                GetMonsterLootGenerationSeed(profile.Seed),
                _settings.MonsterLootBonusCategoryCount,
                _settings.MonsterLootNerfCategoryCount,
                out List<string> monsterLootBonusCategories,
                out List<string> monsterLootNerfCategories
            );

            profile.MonsterLootBonusCategories = monsterLootBonusCategories;
            profile.MonsterLootNerfCategories = monsterLootNerfCategories;
            profile.MonsterLootBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.MonsterLootSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in monsterLootBonusCategories)
            {
                profile.MonsterLootSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.MonsterLootBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in monsterLootNerfCategories)
            {
                profile.MonsterLootSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.MonsterLootBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        public void PopulateEquipmentSelections(SaveEconomyProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            List<string> equipmentSellKeys = EquipmentEconomyCategoryRegistry
                .GetRandomizableCategories()
                .Where(definition => definition.SupportsSell)
                .Select(definition => definition.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int requiredCount = _settings.BonusCategoryCount + _settings.NerfCategoryCount;
            if (equipmentSellKeys.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"Cannot generate equipment profile: requires {requiredCount} sell categories but only {equipmentSellKeys.Count} are registered."
                );
            }

            SelectCategories(
                equipmentSellKeys,
                GetEquipmentGenerationSeed(profile.Seed),
                out List<string> equipmentBonusCategories,
                out List<string> equipmentNerfCategories
            );

            profile.EquipmentBonusCategories = equipmentBonusCategories;
            profile.EquipmentNerfCategories = equipmentNerfCategories;
            profile.EquipmentBuyMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            profile.EquipmentSellMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (string category in equipmentBonusCategories)
            {
                profile.EquipmentSellMultipliers[category] = _settings.BonusSellMultiplier;
                profile.EquipmentBuyMultipliers[category] = _settings.BonusBuyMultiplier == 1f
                    ? _settings.BonusSellMultiplier
                    : _settings.BonusBuyMultiplier;
            }

            foreach (string category in equipmentNerfCategories)
            {
                profile.EquipmentSellMultipliers[category] = _settings.NerfSellMultiplier;
                profile.EquipmentBuyMultipliers[category] = _settings.NerfBuyMultiplier == 1f
                    ? _settings.NerfSellMultiplier
                    : _settings.NerfBuyMultiplier;
            }
        }

        private void SelectCategories(
            List<string> sellKeys,
            int seed,
            out List<string> bonusCategories,
            out List<string> nerfCategories
        )
        {
            SelectCategories(
                sellKeys,
                seed,
                _settings.BonusCategoryCount,
                _settings.NerfCategoryCount,
                out bonusCategories,
                out nerfCategories
            );
        }

        private static void SelectCategories(
            List<string> sellKeys,
            int seed,
            int bonusCategoryCount,
            int nerfCategoryCount,
            out List<string> bonusCategories,
            out List<string> nerfCategories
        )
        {
            List<string> shuffledKeys = new(sellKeys);
            Random rng = new(seed);
            ShuffleInPlace(shuffledKeys, rng);

            bonusCategories = shuffledKeys
                .Take(bonusCategoryCount)
                .ToList();

            nerfCategories = shuffledKeys
                .Skip(bonusCategoryCount)
                .Take(nerfCategoryCount)
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

        private static int GetAnimalProductGenerationSeed(int seed)
        {
            return unchecked((seed * 983) ^ 0x6B8B4567);
        }

        private static int GetForageableGenerationSeed(int seed)
        {
            return unchecked((seed * 1187) ^ 0x45D9F3B);
        }

        private static int GetArtisanGoodGenerationSeed(int seed)
        {
            return unchecked((seed * 1597) ^ 0x27D4EB2D);
        }

        private static int GetPlantExtraGenerationSeed(int seed)
        {
            return unchecked((seed * 1451) ^ 0x3F84D5B5);
        }

        private static int GetCookingFoodGenerationSeed(int seed)
        {
            return unchecked((seed * 1741) ^ 0x52DCE729);
        }

        private static int GetMonsterLootGenerationSeed(int seed)
        {
            return unchecked((seed * 1879) ^ 0x13579BDF);
        }

        private static int GetEquipmentGenerationSeed(int seed)
        {
            return unchecked((seed * 1999) ^ 0x2468ACE1);
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
