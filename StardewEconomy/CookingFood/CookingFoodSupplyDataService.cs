using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for cooking-food supply scores.
    /// </summary>
    internal static class CookingFoodSupplyDataService
    {
        private const string SaveDataKey = "cooking-food-supply";
        internal const float NeutralSupplyScore = CookingFoodMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CookingFoodSupplySaveData? _activeData;

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
                CookingFoodSupplySaveData? loadedData = _helper.Data.ReadSaveData<CookingFoodSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created cooking food supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded cooking food supply data with {_activeData.CookingFoodSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load cooking food supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            CookingFoodSupplySaveData data = EnsureActiveData();
            if (data.CookingFoodSupplyScores.Count == 0)
                return;

            data.CookingFoodSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked cooking food supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.CookingFoodSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.CookingFoodSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            CookingFoodSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(pair.Key, out string normalizedCookingFoodItemId))
                    continue;

                if (!CookingFoodEconomyItemRules.IsCookingFoodItemId(normalizedCookingFoodItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedCookingFoodItemId] = CookingFoodMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.CookingFoodSupplyScores, normalizedScores))
                return false;

            data.CookingFoodSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? cookingFoodItemId)
        {
            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.CookingFoodSupplyScores.TryGetValue(normalizedCookingFoodItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string cookingFoodItemId, float amount, string cookingFoodDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(cookingFoodItemId);

            if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId))
                return 0f;

            if (!CookingFoodEconomyItemRules.IsCookingFoodItemId(normalizedCookingFoodItemId))
                return 0f;

            CookingFoodSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedCookingFoodItemId);
            float updatedScore = CookingFoodMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.CookingFoodSupplyScores[normalizedCookingFoodItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Cooking food supply increased for {FormatCookingFoodLabel(cookingFoodDisplayName, normalizedCookingFoodItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static CookingFoodSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CookingFoodSupplySaveData CreateNewData()
        {
            return new CookingFoodSupplySaveData
            {
                CookingFoodSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static CookingFoodSupplySaveData NormalizeLoadedData(CookingFoodSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            CookingFoodSupplySaveData normalizedData = new()
            {
                CookingFoodSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.CookingFoodSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.CookingFoodSupplyScores)
                {
                    if (!CookingFoodEconomyItemRules.TryNormalizeCookingFoodItemId(pair.Key, out string normalizedCookingFoodItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!CookingFoodEconomyItemRules.IsCookingFoodItemId(normalizedCookingFoodItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = CookingFoodMarketTuning.ClampSupply(pair.Value);
                    normalizedData.CookingFoodSupplyScores[normalizedCookingFoodItemId] = clampedScore;
                    if (!string.Equals(normalizedCookingFoodItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
                        shouldPersist = true;

                    if (clampedScore != pair.Value)
                        shouldPersist = true;
                }
            }

            return normalizedData;
        }

        private static void TryWriteActiveData()
        {
            if (_helper is null || _activeData is null)
                return;

            try
            {
                _helper.Data.WriteSaveData(SaveDataKey, _activeData);
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed writing cooking food supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= CookingFoodMarketTuning.MinSupplyScore;
        }

        private static bool HaveSameScores(
            IReadOnlyDictionary<string, float> currentScores,
            IReadOnlyDictionary<string, float> updatedScores
        )
        {
            if (currentScores.Count != updatedScores.Count)
                return false;

            foreach (KeyValuePair<string, float> pair in currentScores)
            {
                if (!updatedScores.TryGetValue(pair.Key, out float updatedValue))
                    return false;

                if (pair.Value != updatedValue)
                    return false;
            }

            return true;
        }

        private static string FormatCookingFoodLabel(string cookingFoodDisplayName, string cookingFoodItemId)
        {
            return string.IsNullOrWhiteSpace(cookingFoodDisplayName)
                ? cookingFoodItemId
                : $"{cookingFoodDisplayName} ({cookingFoodItemId})";
        }
    }
}
