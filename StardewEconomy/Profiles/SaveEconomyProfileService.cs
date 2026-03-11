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
                        $"Loaded save economy profile seed {normalized.Seed}. Bonuses: [{string.Join(", ", normalized.BonusCategories)}], nerfs: [{string.Join(", ", normalized.NerfCategories)}].",
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
                    $"Generated new save economy profile seed {generatedProfile.Seed}. Bonuses: [{string.Join(", ", generatedProfile.BonusCategories)}], nerfs: [{string.Join(", ", generatedProfile.NerfCategories)}].",
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

        private static float GetTraitModifier(CropEconomicTrait traits, bool useBuySide)
        {
            if (traits == CropEconomicTrait.None || _activeProfile is null)
                return 1f;

            float modifier = 1f;
            Dictionary<string, float> multipliers = useBuySide
                ? _activeProfile.BuyMultipliers
                : _activeProfile.SellMultipliers;

            foreach (RandomizableEconomyCategoryDefinition definition in EconomyCategoryRegistry.GetRandomizableCategories())
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
                BuyMultipliers = new Dictionary<string, float>(KeyComparer),
                SellMultipliers = new Dictionary<string, float>(KeyComparer)
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
                BuyMultipliers = new Dictionary<string, float>(KeyComparer),
                SellMultipliers = new Dictionary<string, float>(KeyComparer)
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
                    && EconomyCategoryRegistry.TryGetCategory(category, out RandomizableEconomyCategoryDefinition definition)
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
                    && EconomyCategoryRegistry.TryGetCategory(category, out RandomizableEconomyCategoryDefinition definition)
                    && definition.SupportsBuy
                    && (!normalizedProfile.BuyMultipliers.TryGetValue(category, out float buyMultiplier)
                        || !IsValidMultiplier(buyMultiplier)))
                {
                    normalizedProfile.BuyMultipliers[category] = GenerationSettings.NerfBuyMultiplier;
                    shouldPersist = true;
                }
            }

            if (!string.Equals(normalizedProfile.ProfileId, loadedProfile.ProfileId, StringComparison.Ordinal)
                || !HaveSameCategoryOrder(loadedProfile.BonusCategories, bonusCategories)
                || !HaveSameCategoryOrder(loadedProfile.NerfCategories, nerfCategories))
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

                if (!EconomyCategoryRegistry.TryGetCategory(rawCategory, out RandomizableEconomyCategoryDefinition definition))
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
                if (!EconomyCategoryRegistry.TryGetCategory(pair.Key, out RandomizableEconomyCategoryDefinition definition))
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
