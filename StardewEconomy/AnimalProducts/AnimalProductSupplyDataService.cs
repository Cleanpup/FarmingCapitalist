using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for animal product supply scores.
    /// </summary>
    internal static class AnimalProductSupplyDataService
    {
        private const string SaveDataKey = "animal-product-supply";
        internal const float NeutralSupplyScore = AnimalProductMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static AnimalProductSupplySaveData? _activeData;

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
                AnimalProductSupplySaveData? loadedData = _helper.Data.ReadSaveData<AnimalProductSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created animal product supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded animal product supply data with {_activeData.AnimalProductSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load animal product supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            AnimalProductSupplySaveData data = EnsureActiveData();
            if (data.AnimalProductSupplyScores.Count == 0)
                return;

            data.AnimalProductSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked animal product supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.AnimalProductSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.AnimalProductSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            AnimalProductSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(pair.Key, out string normalizedAnimalProductItemId))
                    continue;

                if (!AnimalProductEconomyItemRules.IsAnimalProductItemId(normalizedAnimalProductItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedAnimalProductItemId] = AnimalProductMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.AnimalProductSupplyScores, normalizedScores))
                return false;

            data.AnimalProductSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? animalProductItemId)
        {
            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.AnimalProductSupplyScores.TryGetValue(normalizedAnimalProductItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string animalProductItemId, float amount, string animalProductDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(animalProductItemId);

            if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(animalProductItemId, out string normalizedAnimalProductItemId))
                return 0f;

            if (!AnimalProductEconomyItemRules.IsAnimalProductItemId(normalizedAnimalProductItemId))
                return 0f;

            AnimalProductSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedAnimalProductItemId);
            float updatedScore = AnimalProductMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.AnimalProductSupplyScores[normalizedAnimalProductItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Animal product supply increased for {FormatAnimalProductLabel(animalProductDisplayName, normalizedAnimalProductItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static AnimalProductSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static AnimalProductSupplySaveData CreateNewData()
        {
            return new AnimalProductSupplySaveData
            {
                AnimalProductSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static AnimalProductSupplySaveData NormalizeLoadedData(AnimalProductSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            AnimalProductSupplySaveData normalizedData = new()
            {
                AnimalProductSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.AnimalProductSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.AnimalProductSupplyScores)
                {
                    if (!AnimalProductEconomyItemRules.TryNormalizeAnimalProductItemId(pair.Key, out string normalizedAnimalProductItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!AnimalProductEconomyItemRules.IsAnimalProductItemId(normalizedAnimalProductItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = AnimalProductMarketTuning.ClampSupply(pair.Value);
                    normalizedData.AnimalProductSupplyScores[normalizedAnimalProductItemId] = clampedScore;
                    if (!string.Equals(normalizedAnimalProductItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing animal product supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= AnimalProductMarketTuning.MinSupplyScore;
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

        private static string FormatAnimalProductLabel(string animalProductDisplayName, string animalProductItemId)
        {
            return string.IsNullOrWhiteSpace(animalProductDisplayName)
                ? animalProductItemId
                : $"{animalProductDisplayName} ({animalProductItemId})";
        }
    }
}
