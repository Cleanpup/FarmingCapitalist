using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for plantExtra supply scores.
    /// </summary>
    internal static class PlantExtraSupplyDataService
    {
        private const string SaveDataKey = "plant-extra-supply";
        internal const float NeutralSupplyScore = PlantExtraMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static PlantExtraSupplySaveData? _activeData;

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
                PlantExtraSupplySaveData? loadedData = _helper.Data.ReadSaveData<PlantExtraSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created plantExtra supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded plantExtra supply data with {_activeData.PlantExtraSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load plantExtra supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            PlantExtraSupplySaveData data = EnsureActiveData();
            if (data.PlantExtraSupplyScores.Count == 0)
                return;

            data.PlantExtraSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked plantExtra supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.PlantExtraSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.PlantExtraSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            PlantExtraSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(pair.Key, out string normalizedPlantExtraItemId))
                    continue;

                if (!PlantExtraEconomyItemRules.IsPlantExtraItemId(normalizedPlantExtraItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedPlantExtraItemId] = PlantExtraMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.PlantExtraSupplyScores, normalizedScores))
                return false;

            data.PlantExtraSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? plantExtraItemId)
        {
            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.PlantExtraSupplyScores.TryGetValue(normalizedPlantExtraItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string plantExtraItemId, float amount, string plantExtraDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(plantExtraItemId);

            if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(plantExtraItemId, out string normalizedPlantExtraItemId))
                return 0f;

            if (!PlantExtraEconomyItemRules.IsPlantExtraItemId(normalizedPlantExtraItemId))
                return 0f;

            PlantExtraSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedPlantExtraItemId);
            float updatedScore = PlantExtraMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.PlantExtraSupplyScores[normalizedPlantExtraItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"PlantExtra supply increased for {FormatPlantExtraLabel(plantExtraDisplayName, normalizedPlantExtraItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static PlantExtraSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static PlantExtraSupplySaveData CreateNewData()
        {
            return new PlantExtraSupplySaveData
            {
                PlantExtraSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static PlantExtraSupplySaveData NormalizeLoadedData(PlantExtraSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            PlantExtraSupplySaveData normalizedData = new()
            {
                PlantExtraSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.PlantExtraSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.PlantExtraSupplyScores)
                {
                    if (!PlantExtraEconomyItemRules.TryNormalizePlantExtraItemId(pair.Key, out string normalizedPlantExtraItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!PlantExtraEconomyItemRules.IsPlantExtraItemId(normalizedPlantExtraItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = PlantExtraMarketTuning.ClampSupply(pair.Value);
                    normalizedData.PlantExtraSupplyScores[normalizedPlantExtraItemId] = clampedScore;
                    if (!string.Equals(normalizedPlantExtraItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing plantExtra supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= PlantExtraMarketTuning.MinSupplyScore;
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

        private static string FormatPlantExtraLabel(string plantExtraDisplayName, string plantExtraItemId)
        {
            return string.IsNullOrWhiteSpace(plantExtraDisplayName)
                ? plantExtraItemId
                : $"{plantExtraDisplayName} ({plantExtraItemId})";
        }
    }
}
