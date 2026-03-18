using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for forageable supply scores.
    /// </summary>
    internal static class ForageableSupplyDataService
    {
        private const string SaveDataKey = "forageable-supply";
        internal const float NeutralSupplyScore = ForageableMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static ForageableSupplySaveData? _activeData;

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
                ForageableSupplySaveData? loadedData = _helper.Data.ReadSaveData<ForageableSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created forageable supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded forageable supply data with {_activeData.ForageableSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load forageable supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            ForageableSupplySaveData data = EnsureActiveData();
            if (data.ForageableSupplyScores.Count == 0)
                return;

            data.ForageableSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked forageable supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.ForageableSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.ForageableSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            ForageableSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(pair.Key, out string normalizedForageableItemId))
                    continue;

                if (!ForageableEconomyItemRules.IsForageableItemId(normalizedForageableItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedForageableItemId] = ForageableMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.ForageableSupplyScores, normalizedScores))
                return false;

            data.ForageableSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? forageableItemId)
        {
            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.ForageableSupplyScores.TryGetValue(normalizedForageableItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string forageableItemId, float amount, string forageableDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(forageableItemId);

            if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(forageableItemId, out string normalizedForageableItemId))
                return 0f;

            if (!ForageableEconomyItemRules.IsForageableItemId(normalizedForageableItemId))
                return 0f;

            ForageableSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedForageableItemId);
            float updatedScore = ForageableMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.ForageableSupplyScores[normalizedForageableItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Forageable supply increased for {FormatForageableLabel(forageableDisplayName, normalizedForageableItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static ForageableSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static ForageableSupplySaveData CreateNewData()
        {
            return new ForageableSupplySaveData
            {
                ForageableSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static ForageableSupplySaveData NormalizeLoadedData(ForageableSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            ForageableSupplySaveData normalizedData = new()
            {
                ForageableSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.ForageableSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.ForageableSupplyScores)
                {
                    if (!ForageableEconomyItemRules.TryNormalizeForageableItemId(pair.Key, out string normalizedForageableItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!ForageableEconomyItemRules.IsForageableItemId(normalizedForageableItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = ForageableMarketTuning.ClampSupply(pair.Value);
                    normalizedData.ForageableSupplyScores[normalizedForageableItemId] = clampedScore;
                    if (!string.Equals(normalizedForageableItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing forageable supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= ForageableMarketTuning.MinSupplyScore;
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

        private static string FormatForageableLabel(string forageableDisplayName, string forageableItemId)
        {
            return string.IsNullOrWhiteSpace(forageableDisplayName)
                ? forageableItemId
                : $"{forageableDisplayName} ({forageableItemId})";
        }
    }
}
