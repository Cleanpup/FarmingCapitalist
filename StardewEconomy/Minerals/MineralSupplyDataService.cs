using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for mineral supply scores.
    /// </summary>
    internal static class MineralSupplyDataService
    {
        private const string SaveDataKey = "mineral-supply";
        internal const float NeutralSupplyScore = MineralMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static MineralSupplySaveData? _activeData;

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
                MineralSupplySaveData? loadedData = _helper.Data.ReadSaveData<MineralSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created mining supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded mining supply data with {_activeData.MineralSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load mining supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            MineralSupplySaveData data = EnsureActiveData();
            if (data.MineralSupplyScores.Count == 0)
                return;

            data.MineralSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked mining supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.MineralSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.MineralSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            MineralSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!MineralEconomyItemRules.TryNormalizeMineralItemId(pair.Key, out string normalizedMineralItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedMineralItemId] = pair.Value;
            }

            if (HaveSameScores(data.MineralSupplyScores, normalizedScores))
                return false;

            data.MineralSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? mineralItemId)
        {
            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.MineralSupplyScores.TryGetValue(normalizedMineralItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string mineralItemId, float amount, string mineralDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(mineralItemId);

            if (!MineralEconomyItemRules.TryNormalizeMineralItemId(mineralItemId, out string normalizedMineralItemId))
                return 0f;

            MineralSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedMineralItemId);

            float updatedScore = previousScore + amount;
            if (updatedScore == previousScore)
                return updatedScore;

            data.MineralSupplyScores[normalizedMineralItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Mining supply increased for {FormatMineralLabel(mineralDisplayName, normalizedMineralItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static MineralSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static MineralSupplySaveData CreateNewData()
        {
            return new MineralSupplySaveData
            {
                MineralSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static MineralSupplySaveData NormalizeLoadedData(MineralSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            MineralSupplySaveData normalizedData = new()
            {
                MineralSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.MineralSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.MineralSupplyScores)
                {
                    if (!MineralEconomyItemRules.TryNormalizeMineralItemId(pair.Key, out string normalizedMineralItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    normalizedData.MineralSupplyScores[normalizedMineralItemId] = pair.Value;
                    if (!string.Equals(normalizedMineralItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing mining supply data: {ex}", LogLevel.Error);
            }
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

        private static string FormatMineralLabel(string mineralDisplayName, string mineralItemId)
        {
            return string.IsNullOrWhiteSpace(mineralDisplayName)
                ? mineralItemId
                : $"{mineralDisplayName} ({mineralItemId})";
        }
    }
}
