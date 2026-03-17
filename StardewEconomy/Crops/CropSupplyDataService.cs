using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for crop supply scores.
    /// </summary>
    internal static class CropSupplyDataService
    {
        private const string SaveDataKey = "crop-supply";
        internal const float NeutralSupplyScore = CropMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CropSupplySaveData? _activeData;

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
                CropSupplySaveData? loadedData = _helper.Data.ReadSaveData<CropSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Created crop supply data for this save. LastDecayDay={_activeData.LastDecayDay}.",
                    LogLevel.Trace
                );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded crop supply data with {_activeData.CropSupplyScores.Count} tracked crops. LastDecayDay={_activeData.LastDecayDay}.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load crop supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            CropSupplySaveData data = EnsureActiveData();
            if (data.CropSupplyScores.Count == 0)
                return;

            data.CropSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked crop supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.CropSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.CropSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            CropSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!TryNormalizeProduceItemId(pair.Key, out string normalizedProduceItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedProduceItemId] = pair.Value;
            }

            if (HaveSameScores(data.CropSupplyScores, normalizedScores))
                return false;

            data.CropSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? cropProduceItemId)
        {
            if (!TryNormalizeProduceItemId(cropProduceItemId, out string normalizedProduceItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.CropSupplyScores.TryGetValue(normalizedProduceItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string cropProduceItemId, float amount, string cropDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(cropProduceItemId);

            if (!TryNormalizeProduceItemId(cropProduceItemId, out string normalizedProduceItemId))
                return 0f;

            CropSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedProduceItemId);

            float updatedScore = previousScore + amount;
            if (updatedScore == previousScore)
                return updatedScore;

            data.CropSupplyScores[normalizedProduceItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Crop supply increased for {FormatCropLabel(cropDisplayName, normalizedProduceItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static CropSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CropSupplySaveData CreateNewData()
        {
            return new CropSupplySaveData
            {
                CropSupplyScores = new Dictionary<string, float>(KeyComparer),
                LastDecayDay = GetCurrentDayKey()
            };
        }

        private static CropSupplySaveData NormalizeLoadedData(CropSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            CropSupplySaveData normalizedData = new()
            {
                CropSupplyScores = new Dictionary<string, float>(KeyComparer),
                LastDecayDay = loadedData.LastDecayDay
            };

            if (loadedData.CropSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.CropSupplyScores)
                {
                    if (!TryNormalizeProduceItemId(pair.Key, out string normalizedProduceItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    normalizedData.CropSupplyScores[normalizedProduceItemId] = pair.Value;
                    if (!string.Equals(normalizedProduceItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
                        shouldPersist = true;
                }
            }

            if (normalizedData.LastDecayDay < 0)
            {
                int currentDay = GetCurrentDayKey();
                if (currentDay >= 0)
                {
                    normalizedData.LastDecayDay = currentDay;
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
                _monitor?.Log($"Failed writing crop supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool TryNormalizeProduceItemId(string? rawProduceItemId, out string normalizedProduceItemId)
        {
            normalizedProduceItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawProduceItemId))
                return false;

            string candidate = rawProduceItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedProduceItemId = candidate;
            return true;
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= 0f;
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

        private static int GetCurrentDayKey()
        {
            return Context.IsWorldReady
                ? Game1.Date.TotalDays
                : -1;
        }

        private static string FormatCropLabel(string cropDisplayName, string cropProduceItemId)
        {
            return string.IsNullOrWhiteSpace(cropDisplayName)
                ? cropProduceItemId
                : $"{cropDisplayName} ({cropProduceItemId})";
        }
    }
}
