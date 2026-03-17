using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for fish supply scores.
    /// This stays separate from crop supply persistence for now.
    /// </summary>
    internal static class FishSupplyDataService
    {
        private const string SaveDataKey = "fish-supply";
        internal const float NeutralSupplyScore = FishMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static FishSupplySaveData? _activeData;

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
                FishSupplySaveData? loadedData = _helper.Data.ReadSaveData<FishSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log(
                        "Created fish supply data for this save.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded fish supply data with {_activeData.FishSupplyScores.Count} tracked fish.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load fish supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            FishSupplySaveData data = EnsureActiveData();
            if (data.FishSupplyScores.Count == 0)
                return;

            data.FishSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked fish supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.FishSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.FishSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            FishSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!TryNormalizeFishItemId(pair.Key, out string normalizedFishItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedFishItemId] = FishMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.FishSupplyScores, normalizedScores))
                return false;

            data.FishSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? fishItemId)
        {
            if (!TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.FishSupplyScores.TryGetValue(normalizedFishItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string fishItemId, float amount, string fishDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(fishItemId);

            if (!TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return 0f;

            FishSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedFishItemId);

            float updatedScore = FishMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.FishSupplyScores[normalizedFishItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Fish supply increased for {FormatFishLabel(fishDisplayName, normalizedFishItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static FishSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static FishSupplySaveData CreateNewData()
        {
            return new FishSupplySaveData
            {
                FishSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static FishSupplySaveData NormalizeLoadedData(FishSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            FishSupplySaveData normalizedData = new()
            {
                FishSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.FishSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.FishSupplyScores)
                {
                    if (!TryNormalizeFishItemId(pair.Key, out string normalizedFishItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = FishMarketTuning.ClampSupply(pair.Value);
                    normalizedData.FishSupplyScores[normalizedFishItemId] = clampedScore;
                    if (!string.Equals(normalizedFishItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing fish supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool TryNormalizeFishItemId(string? rawFishItemId, out string normalizedFishItemId)
        {
            normalizedFishItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawFishItemId))
                return false;

            string candidate = rawFishItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedFishItemId = candidate;
            return true;
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= FishMarketTuning.MinSupplyScore;
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
        private static string FormatFishLabel(string fishDisplayName, string fishItemId)
        {
            return string.IsNullOrWhiteSpace(fishDisplayName)
                ? fishItemId
                : $"{fishDisplayName} ({fishItemId})";
        }
    }
}
