using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime access for save-specific market profiles.
    /// </summary>
    internal static class SaveEconomyProfileService
    {
        private const string SaveDataKey = "economy-profile";
        private const string DefaultProfileId = "Randomized";

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly EconomyProfileGenerationSettings GenerationSettings = new();
        private static readonly EconomyProfileGenerator Generator = new(GenerationSettings);

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static SaveEconomyProfile? _activeProfile;

        public static void Initialize(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
        }

        public static void LoadOrCreateForCurrentSave()
        {
            if (_helper is null)
                return;

            try
            {
                SaveEconomyProfile? loadedProfile = _helper.Data.ReadSaveData<SaveEconomyProfile>(SaveDataKey);
                if (loadedProfile is not null
                    && TryNormalizeLoadedProfile(loadedProfile, out SaveEconomyProfile normalized, out bool shouldPersist))
                {
                    _activeProfile = normalized;
                    if (shouldPersist)
                        TryWriteProfile(normalized);

                    _monitor?.Log(
                        $"Loaded save economy profile seed {normalized.Seed}. {FormatProfileCategorySummary(normalized)}",
                        LogLevel.Trace
                    );
                    return;
                }

                if (loadedProfile is not null)
                {
                    _monitor?.Log(
                        "Existing save economy profile is missing required categories or contains invalid data. Regenerating profile.",
                        LogLevel.Warn
                    );
                }

                SaveEconomyProfile generatedProfile = GenerateProfile();
                _activeProfile = generatedProfile;
                TryWriteProfile(generatedProfile);

                _monitor?.Log(
                    $"Generated new save economy profile seed {generatedProfile.Seed}. {FormatProfileCategorySummary(generatedProfile)}",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load or generate save economy profile: {ex}", LogLevel.Error);
                _activeProfile = CreateNeutralProfile();
            }
        }

        public static void ClearActiveProfile()
        {
            _activeProfile = null;
        }

        public static float GetBuyModifierForTraits(CropEconomicTrait traits)
        {
            return GetTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(CropEconomicTrait traits)
        {
            return GetTraitModifier(traits, useBuySide: false);
        }

        public static float GetSellModifierForTraits(FishEconomicTrait traits)
        {
            return GetFishTraitModifier(traits);
        }

        public static float GetSellModifierForTraits(MineralEconomicTrait traits)
        {
            return GetMineralTraitModifier(traits);
        }

        public static float GetBuyModifierForTraits(AnimalProductEconomicTrait traits)
        {
            return GetAnimalProductTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(AnimalProductEconomicTrait traits)
        {
            return GetAnimalProductTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(ForageableEconomicTrait traits)
        {
            return GetForageableTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(ForageableEconomicTrait traits)
        {
            return GetForageableTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(PlantExtraEconomicTrait traits)
        {
            return GetPlantExtraTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(PlantExtraEconomicTrait traits)
        {
            return GetPlantExtraTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(CraftingExtraEconomicTrait traits)
        {
            return GetCraftingExtraTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(CraftingExtraEconomicTrait traits)
        {
            return GetCraftingExtraTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(ArtisanGoodEconomicTrait traits)
        {
            return GetArtisanGoodTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(ArtisanGoodEconomicTrait traits)
        {
            return GetArtisanGoodTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(CookingFoodEconomicTrait traits)
        {
            return GetCookingFoodTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(CookingFoodEconomicTrait traits)
        {
            return GetCookingFoodTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(MonsterLootEconomicTrait traits)
        {
            return GetMonsterLootTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(MonsterLootEconomicTrait traits)
        {
            return GetMonsterLootTraitModifier(traits, useBuySide: false);
        }

        public static float GetBuyModifierForTraits(EquipmentEconomicTrait traits)
        {
            return GetEquipmentTraitModifier(traits, useBuySide: true);
        }

        public static float GetSellModifierForTraits(EquipmentEconomicTrait traits)
        {
            return GetEquipmentTraitModifier(traits, useBuySide: false);
        }

        // Keep this summary in sync when adding randomized category families so debug logs stay complete.
        private static string FormatProfileCategorySummary(SaveEconomyProfile profile)
        {
            return $"Crop bonuses: [{string.Join(", ", profile.BonusCategories)}], crop nerfs: [{string.Join(", ", profile.NerfCategories)}], fish bonuses: [{string.Join(", ", profile.FishBonusCategories)}], fish nerfs: [{string.Join(", ", profile.FishNerfCategories)}], mining bonuses: [{string.Join(", ", profile.MineralBonusCategories)}], mining nerfs: [{string.Join(", ", profile.MineralNerfCategories)}], animal product bonuses: [{string.Join(", ", profile.AnimalProductBonusCategories)}], animal product nerfs: [{string.Join(", ", profile.AnimalProductNerfCategories)}], forageable bonuses: [{string.Join(", ", profile.ForageableBonusCategories)}], forageable nerfs: [{string.Join(", ", profile.ForageableNerfCategories)}], plant-extra bonuses: [{string.Join(", ", profile.PlantExtraBonusCategories)}], plant-extra nerfs: [{string.Join(", ", profile.PlantExtraNerfCategories)}], crafting-extra bonuses: [{string.Join(", ", profile.CraftingExtraBonusCategories)}], crafting-extra nerfs: [{string.Join(", ", profile.CraftingExtraNerfCategories)}], artisan good bonuses: [{string.Join(", ", profile.ArtisanGoodBonusCategories)}], artisan good nerfs: [{string.Join(", ", profile.ArtisanGoodNerfCategories)}], cooking food bonuses: [{string.Join(", ", profile.CookingFoodBonusCategories)}], cooking food nerfs: [{string.Join(", ", profile.CookingFoodNerfCategories)}], monster-loot bonuses: [{string.Join(", ", profile.MonsterLootBonusCategories)}], monster-loot nerfs: [{string.Join(", ", profile.MonsterLootNerfCategories)}], equipment bonuses: [{string.Join(", ", profile.EquipmentBonusCategories)}], equipment nerfs: [{string.Join(", ", profile.EquipmentNerfCategories)}].";
        }

        private static float GetTraitModifier(CropEconomicTrait traits, bool useBuySide)
        {
            if (traits == CropEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.BuyMultipliers
                : _activeProfile.SellMultipliers;

            foreach (RandomizableCropEconomyCategoryDefinition definition in CropEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetFishTraitModifier(FishEconomicTrait traits)
        {
            if (traits == FishEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            foreach (RandomizableFishEconomyCategoryDefinition definition in FishEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (!definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(_activeProfile.FishSellMultipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetMineralTraitModifier(MineralEconomicTrait traits)
        {
            if (traits == MineralEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            foreach (RandomizableMineralEconomyCategoryDefinition definition in MineralEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (!definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(_activeProfile.MineralSellMultipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetAnimalProductTraitModifier(AnimalProductEconomicTrait traits, bool useBuySide)
        {
            if (traits == AnimalProductEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.AnimalProductBuyMultipliers
                : _activeProfile.AnimalProductSellMultipliers;

            foreach (RandomizableAnimalProductEconomyCategoryDefinition definition in AnimalProductEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetForageableTraitModifier(ForageableEconomicTrait traits, bool useBuySide)
        {
            if (traits == ForageableEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.ForageableBuyMultipliers
                : _activeProfile.ForageableSellMultipliers;

            foreach (RandomizableForageableEconomyCategoryDefinition definition in ForageableEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetPlantExtraTraitModifier(PlantExtraEconomicTrait traits, bool useBuySide)
        {
            if (traits == PlantExtraEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.PlantExtraBuyMultipliers
                : _activeProfile.PlantExtraSellMultipliers;

            foreach (RandomizablePlantExtraEconomyCategoryDefinition definition in PlantExtraEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetCraftingExtraTraitModifier(CraftingExtraEconomicTrait traits, bool useBuySide)
        {
            if (traits == CraftingExtraEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.CraftingExtraBuyMultipliers
                : _activeProfile.CraftingExtraSellMultipliers;

            foreach (RandomizableCraftingExtraEconomyCategoryDefinition definition in CraftingExtraEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetArtisanGoodTraitModifier(ArtisanGoodEconomicTrait traits, bool useBuySide)
        {
            if (traits == ArtisanGoodEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.ArtisanGoodBuyMultipliers
                : _activeProfile.ArtisanGoodSellMultipliers;

            foreach (RandomizableArtisanGoodEconomyCategoryDefinition definition in ArtisanGoodEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetCookingFoodTraitModifier(CookingFoodEconomicTrait traits, bool useBuySide)
        {
            if (traits == CookingFoodEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.CookingFoodBuyMultipliers
                : _activeProfile.CookingFoodSellMultipliers;

            foreach (RandomizableCookingFoodEconomyCategoryDefinition definition in CookingFoodEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetMonsterLootTraitModifier(MonsterLootEconomicTrait traits, bool useBuySide)
        {
            if (traits == MonsterLootEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.MonsterLootBuyMultipliers
                : _activeProfile.MonsterLootSellMultipliers;

            foreach (RandomizableMonsterLootEconomyCategoryDefinition definition in MonsterLootEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetEquipmentTraitModifier(EquipmentEconomicTrait traits, bool useBuySide)
        {
            if (traits == EquipmentEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.EquipmentBuyMultipliers
                : _activeProfile.EquipmentSellMultipliers;

            foreach (RandomizableEquipmentEconomyCategoryDefinition definition in EquipmentEconomyCategoryRegistry.GetRandomizableCategories())
            {
                if (useBuySide && !definition.SupportsBuy)
                    continue;

                if (!useBuySide && !definition.SupportsSell)
                    continue;

                if (!definition.MatchesTraits(traits))
                    continue;

                modifier *= GetCategoryMultiplier(multipliers, definition.Key);
            }

            return modifier;
        }

        private static float GetCategoryMultiplier(Dictionary<string, float>? multipliers, string categoryKey)
        {
            if (multipliers is null)
                return 1f;

            if (!multipliers.TryGetValue(categoryKey, out float value))
                return 1f;

            return IsValidMultiplier(value)
                ? value
                : 1f;
        }

        private static SaveEconomyProfile GenerateProfile()
        {
            int seed = CreateProfileSeed();
            return Generator.Generate(DefaultProfileId, seed);
        }

        private static int CreateProfileSeed()
        {
            ulong saveUniqueId = Game1.uniqueIDForThisGame;
            int randomPart = Random.Shared.Next();
            int tickPart = Environment.TickCount;
            return unchecked((int)(saveUniqueId ^ (ulong)(uint)randomPart ^ (ulong)(uint)tickPart));
        }

        private static SaveEconomyProfile CreateNeutralProfile()
        {
            return new SaveEconomyProfile
            {
                ProfileId = "NeutralFallback",
                Seed = 0,
                BonusCategories = new List<string>(),
                NerfCategories = new List<string>(),
                FishBonusCategories = new List<string>(),
                FishNerfCategories = new List<string>(),
                MineralBonusCategories = new List<string>(),
                MineralNerfCategories = new List<string>(),
                AnimalProductBonusCategories = new List<string>(),
                AnimalProductNerfCategories = new List<string>(),
                ForageableBonusCategories = new List<string>(),
                ForageableNerfCategories = new List<string>(),
                PlantExtraBonusCategories = new List<string>(),
                PlantExtraNerfCategories = new List<string>(),
                CraftingExtraBonusCategories = new List<string>(),
                CraftingExtraNerfCategories = new List<string>(),
                ArtisanGoodBonusCategories = new List<string>(),
                ArtisanGoodNerfCategories = new List<string>(),
                CookingFoodBonusCategories = new List<string>(),
                CookingFoodNerfCategories = new List<string>(),
                MonsterLootBonusCategories = new List<string>(),
                MonsterLootNerfCategories = new List<string>(),
                EquipmentBonusCategories = new List<string>(),
                EquipmentNerfCategories = new List<string>(),
                BuyMultipliers = new Dictionary<string, float>(KeyComparer),
                SellMultipliers = new Dictionary<string, float>(KeyComparer),
                FishSellMultipliers = new Dictionary<string, float>(KeyComparer),
                MineralSellMultipliers = new Dictionary<string, float>(KeyComparer),
                AnimalProductBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                AnimalProductSellMultipliers = new Dictionary<string, float>(KeyComparer),
                ForageableBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                ForageableSellMultipliers = new Dictionary<string, float>(KeyComparer),
                PlantExtraBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                PlantExtraSellMultipliers = new Dictionary<string, float>(KeyComparer),
                CraftingExtraBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                CraftingExtraSellMultipliers = new Dictionary<string, float>(KeyComparer),
                ArtisanGoodBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                ArtisanGoodSellMultipliers = new Dictionary<string, float>(KeyComparer),
                CookingFoodBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                CookingFoodSellMultipliers = new Dictionary<string, float>(KeyComparer),
                MonsterLootBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                MonsterLootSellMultipliers = new Dictionary<string, float>(KeyComparer),
                EquipmentBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                EquipmentSellMultipliers = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static void TryWriteProfile(SaveEconomyProfile profile)
        {
            if (_helper is null)
                return;

            try
            {
                _helper.Data.WriteSaveData(SaveDataKey, profile);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed writing save economy profile data: {ex}", LogLevel.Error);
            }
        }

        private static bool TryNormalizeLoadedProfile(
            SaveEconomyProfile loadedProfile,
            out SaveEconomyProfile normalizedProfile,
            out bool shouldPersist
        )
        {
            shouldPersist = false;
            normalizedProfile = new SaveEconomyProfile
            {
                ProfileId = string.IsNullOrWhiteSpace(loadedProfile.ProfileId)
                    ? DefaultProfileId
                    : loadedProfile.ProfileId,
                Seed = loadedProfile.Seed,
                BonusCategories = new List<string>(),
                NerfCategories = new List<string>(),
                FishBonusCategories = new List<string>(),
                FishNerfCategories = new List<string>(),
                MineralBonusCategories = new List<string>(),
                MineralNerfCategories = new List<string>(),
                AnimalProductBonusCategories = new List<string>(),
                AnimalProductNerfCategories = new List<string>(),
                ForageableBonusCategories = new List<string>(),
                ForageableNerfCategories = new List<string>(),
                PlantExtraBonusCategories = new List<string>(),
                PlantExtraNerfCategories = new List<string>(),
                CraftingExtraBonusCategories = new List<string>(),
                CraftingExtraNerfCategories = new List<string>(),
                ArtisanGoodBonusCategories = new List<string>(),
                ArtisanGoodNerfCategories = new List<string>(),
                CookingFoodBonusCategories = new List<string>(),
                CookingFoodNerfCategories = new List<string>(),
                MonsterLootBonusCategories = new List<string>(),
                MonsterLootNerfCategories = new List<string>(),
                EquipmentBonusCategories = new List<string>(),
                EquipmentNerfCategories = new List<string>(),
                BuyMultipliers = new Dictionary<string, float>(KeyComparer),
                SellMultipliers = new Dictionary<string, float>(KeyComparer),
                FishSellMultipliers = new Dictionary<string, float>(KeyComparer),
                MineralSellMultipliers = new Dictionary<string, float>(KeyComparer),
                AnimalProductBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                AnimalProductSellMultipliers = new Dictionary<string, float>(KeyComparer),
                ForageableBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                ForageableSellMultipliers = new Dictionary<string, float>(KeyComparer),
                PlantExtraBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                PlantExtraSellMultipliers = new Dictionary<string, float>(KeyComparer),
                CraftingExtraBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                CraftingExtraSellMultipliers = new Dictionary<string, float>(KeyComparer),
                ArtisanGoodBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                ArtisanGoodSellMultipliers = new Dictionary<string, float>(KeyComparer),
                CookingFoodBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                CookingFoodSellMultipliers = new Dictionary<string, float>(KeyComparer),
                MonsterLootBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                MonsterLootSellMultipliers = new Dictionary<string, float>(KeyComparer),
                EquipmentBuyMultipliers = new Dictionary<string, float>(KeyComparer),
                EquipmentSellMultipliers = new Dictionary<string, float>(KeyComparer)
            };

            List<string> bonusCategories = NormalizeCategories(loadedProfile.BonusCategories, supportsSell: true);
            HashSet<string> disallowedNerfCategories = new(bonusCategories, KeyComparer);
            List<string> nerfCategories = NormalizeCategories(
                loadedProfile.NerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedNerfCategories
            );

            if (bonusCategories.Count != GenerationSettings.BonusCategoryCount
                || nerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                return false;
            }

            normalizedProfile.BonusCategories = bonusCategories;
            normalizedProfile.NerfCategories = nerfCategories;

            normalizedProfile.BuyMultipliers = NormalizeMultipliers(loadedProfile.BuyMultipliers, supportsBuy: true);
            normalizedProfile.SellMultipliers = NormalizeMultipliers(loadedProfile.SellMultipliers, supportsBuy: false);

            foreach (string category in bonusCategories)
            {
                if (!normalizedProfile.SellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                {
                    normalizedProfile.SellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                    shouldPersist = true;
                }

                if (GenerationSettings.RandomizeBuyMultipliers
                    && CropEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCropEconomyCategoryDefinition definition)
                    && definition.SupportsBuy
                    && (!normalizedProfile.BuyMultipliers.TryGetValue(category, out float buyMultiplier)
                        || !IsValidMultiplier(buyMultiplier)))
                {
                    normalizedProfile.BuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier;
                    shouldPersist = true;
                }
            }

            foreach (string category in nerfCategories)
            {
                if (!normalizedProfile.SellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                {
                    normalizedProfile.SellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                    shouldPersist = true;
                }

                if (GenerationSettings.RandomizeBuyMultipliers
                    && CropEconomyCategoryRegistry.TryGetCategory(category, out RandomizableCropEconomyCategoryDefinition definition)
                    && definition.SupportsBuy
                    && (!normalizedProfile.BuyMultipliers.TryGetValue(category, out float buyMultiplier)
                        || !IsValidMultiplier(buyMultiplier)))
                {
                    normalizedProfile.BuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier;
                    shouldPersist = true;
                }
            }

            List<string> fishBonusCategories = NormalizeFishCategories(loadedProfile.FishBonusCategories, supportsSell: true);
            HashSet<string> disallowedFishNerfCategories = new(fishBonusCategories, KeyComparer);
            List<string> fishNerfCategories = NormalizeFishCategories(
                loadedProfile.FishNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedFishNerfCategories
            );

            if (fishBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || fishNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateFishSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.FishBonusCategories = fishBonusCategories;
                normalizedProfile.FishNerfCategories = fishNerfCategories;
                normalizedProfile.FishSellMultipliers = NormalizeFishMultipliers(loadedProfile.FishSellMultipliers);

                foreach (string category in fishBonusCategories)
                {
                    if (!normalizedProfile.FishSellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                    {
                        normalizedProfile.FishSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in fishNerfCategories)
                {
                    if (!normalizedProfile.FishSellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                    {
                        normalizedProfile.FishSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> mineralBonusCategories = NormalizeMineralCategories(loadedProfile.MineralBonusCategories, supportsSell: true);
            HashSet<string> disallowedMineralNerfCategories = new(mineralBonusCategories, KeyComparer);
            List<string> mineralNerfCategories = NormalizeMineralCategories(
                loadedProfile.MineralNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedMineralNerfCategories
            );

            if (mineralBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || mineralNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateMineralSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.MineralBonusCategories = mineralBonusCategories;
                normalizedProfile.MineralNerfCategories = mineralNerfCategories;
                normalizedProfile.MineralSellMultipliers = NormalizeMineralMultipliers(loadedProfile.MineralSellMultipliers);

                foreach (string category in mineralBonusCategories)
                {
                    if (!normalizedProfile.MineralSellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                    {
                        normalizedProfile.MineralSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in mineralNerfCategories)
                {
                    if (!normalizedProfile.MineralSellMultipliers.TryGetValue(category, out float multiplier) || !IsValidMultiplier(multiplier))
                    {
                        normalizedProfile.MineralSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> animalProductBonusCategories = NormalizeAnimalProductCategories(
                loadedProfile.AnimalProductBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedAnimalProductNerfCategories = new(animalProductBonusCategories, KeyComparer);
            List<string> animalProductNerfCategories = NormalizeAnimalProductCategories(
                loadedProfile.AnimalProductNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedAnimalProductNerfCategories
            );

            if (animalProductBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || animalProductNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateAnimalProductSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.AnimalProductBonusCategories = animalProductBonusCategories;
                normalizedProfile.AnimalProductNerfCategories = animalProductNerfCategories;
                normalizedProfile.AnimalProductBuyMultipliers = NormalizeAnimalProductMultipliers(
                    loadedProfile.AnimalProductBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.AnimalProductSellMultipliers = NormalizeAnimalProductMultipliers(
                    loadedProfile.AnimalProductSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in animalProductBonusCategories)
                {
                    if (!normalizedProfile.AnimalProductSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.AnimalProductSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.AnimalProductBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.AnimalProductBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in animalProductNerfCategories)
                {
                    if (!normalizedProfile.AnimalProductSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.AnimalProductSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.AnimalProductBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.AnimalProductBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> forageableBonusCategories = NormalizeForageableCategories(
                loadedProfile.ForageableBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedForageableNerfCategories = new(forageableBonusCategories, KeyComparer);
            List<string> forageableNerfCategories = NormalizeForageableCategories(
                loadedProfile.ForageableNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedForageableNerfCategories
            );

            if (forageableBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || forageableNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateForageableSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.ForageableBonusCategories = forageableBonusCategories;
                normalizedProfile.ForageableNerfCategories = forageableNerfCategories;
                normalizedProfile.ForageableBuyMultipliers = NormalizeForageableMultipliers(
                    loadedProfile.ForageableBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.ForageableSellMultipliers = NormalizeForageableMultipliers(
                    loadedProfile.ForageableSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in forageableBonusCategories)
                {
                    if (!normalizedProfile.ForageableSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.ForageableSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.ForageableBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.ForageableBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in forageableNerfCategories)
                {
                    if (!normalizedProfile.ForageableSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.ForageableSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.ForageableBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.ForageableBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> plantExtraBonusCategories = NormalizePlantExtraCategories(
                loadedProfile.PlantExtraBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedPlantExtraNerfCategories = new(plantExtraBonusCategories, KeyComparer);
            List<string> plantExtraNerfCategories = NormalizePlantExtraCategories(
                loadedProfile.PlantExtraNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedPlantExtraNerfCategories
            );

            if (plantExtraBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || plantExtraNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulatePlantExtraSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.PlantExtraBonusCategories = plantExtraBonusCategories;
                normalizedProfile.PlantExtraNerfCategories = plantExtraNerfCategories;
                normalizedProfile.PlantExtraBuyMultipliers = NormalizePlantExtraMultipliers(
                    loadedProfile.PlantExtraBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.PlantExtraSellMultipliers = NormalizePlantExtraMultipliers(
                    loadedProfile.PlantExtraSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in plantExtraBonusCategories)
                {
                    if (!normalizedProfile.PlantExtraSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.PlantExtraSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.PlantExtraBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.PlantExtraBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in plantExtraNerfCategories)
                {
                    if (!normalizedProfile.PlantExtraSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.PlantExtraSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.PlantExtraBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.PlantExtraBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            if ((loadedProfile.CraftingExtraBonusCategories?.Count ?? 0) > 0
                || (loadedProfile.CraftingExtraNerfCategories?.Count ?? 0) > 0
                || loadedProfile.CraftingExtraBuyMultipliers is null
                || loadedProfile.CraftingExtraSellMultipliers is null
                || loadedProfile.CraftingExtraBuyMultipliers.Count > 0
                || loadedProfile.CraftingExtraSellMultipliers.Count > 0)
            {
                shouldPersist = true;
            }

            normalizedProfile.CraftingExtraBonusCategories = new List<string>();
            normalizedProfile.CraftingExtraNerfCategories = new List<string>();
            normalizedProfile.CraftingExtraBuyMultipliers = new Dictionary<string, float>(KeyComparer);
            normalizedProfile.CraftingExtraSellMultipliers = new Dictionary<string, float>(KeyComparer);

            List<string> artisanGoodBonusCategories = NormalizeArtisanGoodCategories(
                loadedProfile.ArtisanGoodBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedArtisanGoodNerfCategories = new(artisanGoodBonusCategories, KeyComparer);
            List<string> artisanGoodNerfCategories = NormalizeArtisanGoodCategories(
                loadedProfile.ArtisanGoodNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedArtisanGoodNerfCategories
            );

            if (artisanGoodBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || artisanGoodNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateArtisanGoodSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.ArtisanGoodBonusCategories = artisanGoodBonusCategories;
                normalizedProfile.ArtisanGoodNerfCategories = artisanGoodNerfCategories;
                normalizedProfile.ArtisanGoodBuyMultipliers = NormalizeArtisanGoodMultipliers(
                    loadedProfile.ArtisanGoodBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.ArtisanGoodSellMultipliers = NormalizeArtisanGoodMultipliers(
                    loadedProfile.ArtisanGoodSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in artisanGoodBonusCategories)
                {
                    if (!normalizedProfile.ArtisanGoodSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.ArtisanGoodSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.ArtisanGoodBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.ArtisanGoodBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in artisanGoodNerfCategories)
                {
                    if (!normalizedProfile.ArtisanGoodSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.ArtisanGoodSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.ArtisanGoodBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.ArtisanGoodBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> cookingFoodBonusCategories = NormalizeCookingFoodCategories(
                loadedProfile.CookingFoodBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedCookingFoodNerfCategories = new(cookingFoodBonusCategories, KeyComparer);
            List<string> cookingFoodNerfCategories = NormalizeCookingFoodCategories(
                loadedProfile.CookingFoodNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedCookingFoodNerfCategories
            );

            if (cookingFoodBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || cookingFoodNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateCookingFoodSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.CookingFoodBonusCategories = cookingFoodBonusCategories;
                normalizedProfile.CookingFoodNerfCategories = cookingFoodNerfCategories;
                normalizedProfile.CookingFoodBuyMultipliers = NormalizeCookingFoodMultipliers(
                    loadedProfile.CookingFoodBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.CookingFoodSellMultipliers = NormalizeCookingFoodMultipliers(
                    loadedProfile.CookingFoodSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in cookingFoodBonusCategories)
                {
                    if (!normalizedProfile.CookingFoodSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.CookingFoodSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.CookingFoodBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.CookingFoodBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in cookingFoodNerfCategories)
                {
                    if (!normalizedProfile.CookingFoodSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.CookingFoodSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.CookingFoodBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.CookingFoodBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> monsterLootBonusCategories = NormalizeMonsterLootCategories(
                loadedProfile.MonsterLootBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedMonsterLootNerfCategories = new(monsterLootBonusCategories, KeyComparer);
            List<string> monsterLootNerfCategories = NormalizeMonsterLootCategories(
                loadedProfile.MonsterLootNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedMonsterLootNerfCategories
            );

            if (monsterLootBonusCategories.Count != GenerationSettings.MonsterLootBonusCategoryCount
                || monsterLootNerfCategories.Count != GenerationSettings.MonsterLootNerfCategoryCount)
            {
                Generator.PopulateMonsterLootSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.MonsterLootBonusCategories = monsterLootBonusCategories;
                normalizedProfile.MonsterLootNerfCategories = monsterLootNerfCategories;
                normalizedProfile.MonsterLootBuyMultipliers = NormalizeMonsterLootMultipliers(
                    loadedProfile.MonsterLootBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.MonsterLootSellMultipliers = NormalizeMonsterLootMultipliers(
                    loadedProfile.MonsterLootSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in monsterLootBonusCategories)
                {
                    if (!normalizedProfile.MonsterLootSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.MonsterLootSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.MonsterLootBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.MonsterLootBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in monsterLootNerfCategories)
                {
                    if (!normalizedProfile.MonsterLootSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.MonsterLootSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.MonsterLootBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.MonsterLootBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            List<string> equipmentBonusCategories = NormalizeEquipmentCategories(
                loadedProfile.EquipmentBonusCategories,
                supportsSell: true
            );
            HashSet<string> disallowedEquipmentNerfCategories = new(equipmentBonusCategories, KeyComparer);
            List<string> equipmentNerfCategories = NormalizeEquipmentCategories(
                loadedProfile.EquipmentNerfCategories,
                supportsSell: true,
                disallowedCategories: disallowedEquipmentNerfCategories
            );

            if (equipmentBonusCategories.Count != GenerationSettings.BonusCategoryCount
                || equipmentNerfCategories.Count != GenerationSettings.NerfCategoryCount)
            {
                Generator.PopulateEquipmentSelections(normalizedProfile);
                shouldPersist = true;
            }
            else
            {
                normalizedProfile.EquipmentBonusCategories = equipmentBonusCategories;
                normalizedProfile.EquipmentNerfCategories = equipmentNerfCategories;
                normalizedProfile.EquipmentBuyMultipliers = NormalizeEquipmentMultipliers(
                    loadedProfile.EquipmentBuyMultipliers,
                    supportsBuy: true
                );
                normalizedProfile.EquipmentSellMultipliers = NormalizeEquipmentMultipliers(
                    loadedProfile.EquipmentSellMultipliers,
                    supportsBuy: false
                );

                foreach (string category in equipmentBonusCategories)
                {
                    if (!normalizedProfile.EquipmentSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.EquipmentSellMultipliers[category] = GenerationSettings.BonusSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.EquipmentBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.EquipmentBuyMultipliers[category] = GenerationSettings.BonusBuyMultiplier == 1f
                            ? GenerationSettings.BonusSellMultiplier
                            : GenerationSettings.BonusBuyMultiplier;
                        shouldPersist = true;
                    }
                }

                foreach (string category in equipmentNerfCategories)
                {
                    if (!normalizedProfile.EquipmentSellMultipliers.TryGetValue(category, out float sellMultiplier) || !IsValidMultiplier(sellMultiplier))
                    {
                        normalizedProfile.EquipmentSellMultipliers[category] = GenerationSettings.NerfSellMultiplier;
                        shouldPersist = true;
                    }

                    if (!normalizedProfile.EquipmentBuyMultipliers.TryGetValue(category, out float buyMultiplier) || !IsValidMultiplier(buyMultiplier))
                    {
                        normalizedProfile.EquipmentBuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier == 1f
                            ? GenerationSettings.NerfSellMultiplier
                            : GenerationSettings.NerfBuyMultiplier;
                        shouldPersist = true;
                    }
                }
            }

            if (!string.Equals(normalizedProfile.ProfileId, loadedProfile.ProfileId, StringComparison.Ordinal)
                || !HaveSameCategoryOrder(loadedProfile.BonusCategories, bonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.NerfCategories, nerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.FishBonusCategories, normalizedProfile.FishBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.FishNerfCategories, normalizedProfile.FishNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.MineralBonusCategories, normalizedProfile.MineralBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.MineralNerfCategories, normalizedProfile.MineralNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.AnimalProductBonusCategories, normalizedProfile.AnimalProductBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.AnimalProductNerfCategories, normalizedProfile.AnimalProductNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.ForageableBonusCategories, normalizedProfile.ForageableBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.ForageableNerfCategories, normalizedProfile.ForageableNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.PlantExtraBonusCategories, normalizedProfile.PlantExtraBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.PlantExtraNerfCategories, normalizedProfile.PlantExtraNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.CraftingExtraBonusCategories, normalizedProfile.CraftingExtraBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.CraftingExtraNerfCategories, normalizedProfile.CraftingExtraNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.ArtisanGoodBonusCategories, normalizedProfile.ArtisanGoodBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.ArtisanGoodNerfCategories, normalizedProfile.ArtisanGoodNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.CookingFoodBonusCategories, normalizedProfile.CookingFoodBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.CookingFoodNerfCategories, normalizedProfile.CookingFoodNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.MonsterLootBonusCategories, normalizedProfile.MonsterLootBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.MonsterLootNerfCategories, normalizedProfile.MonsterLootNerfCategories)
                || !HaveSameCategoryOrder(loadedProfile.EquipmentBonusCategories, normalizedProfile.EquipmentBonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.EquipmentNerfCategories, normalizedProfile.EquipmentNerfCategories))
            {
                shouldPersist = true;
            }

            return true;
        }

        private static List<string> NormalizeCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!CropEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableCropEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeFishCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!FishEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableFishEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeMineralCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!MineralEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableMineralEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeAnimalProductCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!AnimalProductEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableAnimalProductEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeForageableCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!ForageableEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableForageableEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizePlantExtraCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!PlantExtraEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizablePlantExtraEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeArtisanGoodCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!ArtisanGoodEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableArtisanGoodEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeCookingFoodCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!CookingFoodEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableCookingFoodEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeMonsterLootCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!MonsterLootEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableMonsterLootEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static List<string> NormalizeEquipmentCategories(
            IEnumerable<string>? rawCategories,
            bool supportsSell,
            ISet<string>? disallowedCategories = null
        )
        {
            List<string> normalized = new();
            if (rawCategories is null)
                return normalized;

            HashSet<string> seen = new(KeyComparer);
            foreach (string? rawCategory in rawCategories)
            {
                if (string.IsNullOrWhiteSpace(rawCategory))
                    continue;

                if (!EquipmentEconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableEquipmentEconomyCategoryDefinition definition))
                    continue;

                if (supportsSell && !definition.SupportsSell)
                    continue;

                if (disallowedCategories is not null && disallowedCategories.Contains(definition.Key))
                    continue;

                if (!seen.Add(definition.Key))
                    continue;

                normalized.Add(definition.Key);
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!CropEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableCropEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeFishMultipliers(IDictionary<string, float>? rawMultipliers)
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!FishEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableFishEconomyCategoryDefinition definition))
                    continue;

                if (!definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeMineralMultipliers(IDictionary<string, float>? rawMultipliers)
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!MineralEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableMineralEconomyCategoryDefinition definition))
                    continue;

                if (!definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeAnimalProductMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!AnimalProductEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableAnimalProductEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeForageableMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!ForageableEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableForageableEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizePlantExtraMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!PlantExtraEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizablePlantExtraEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeArtisanGoodMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!ArtisanGoodEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableArtisanGoodEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeCookingFoodMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!CookingFoodEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableCookingFoodEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeMonsterLootMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!MonsterLootEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableMonsterLootEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static Dictionary<string, float> NormalizeEquipmentMultipliers(
            IDictionary<string, float>? rawMultipliers,
            bool supportsBuy
        )
        {
            Dictionary<string, float> normalized = new(KeyComparer);
            if (rawMultipliers is null)
                return normalized;

            foreach (KeyValuePair<string, float> pair in rawMultipliers)
            {
                if (!EquipmentEconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableEquipmentEconomyCategoryDefinition definition))
                    continue;

                if (supportsBuy && !definition.SupportsBuy)
                    continue;

                if (!supportsBuy && !definition.SupportsSell)
                    continue;

                if (!IsValidMultiplier(pair.Value))
                    continue;

                normalized[definition.Key] = pair.Value;
            }

            return normalized;
        }

        private static bool HaveSameCategoryOrder(IEnumerable<string>? original, IReadOnlyList<string> normalized)
        {
            if (original is null)
                return normalized.Count == 0;

            List<string> originalList = original.ToList();
            if (originalList.Count != normalized.Count)
                return false;

            for (int i = 0; i < originalList.Count; i++)
            {
                if (!string.Equals(originalList[i], normalized[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool IsValidMultiplier(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
