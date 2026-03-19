using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for equipment supply scores.
    /// </summary>
    internal static class EquipmentSupplyDataService
    {
        private const string SaveDataKey = "equipment-supply";
        internal const float NeutralSupplyScore = EquipmentMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static EquipmentSupplySaveData? _activeData;

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
                EquipmentSupplySaveData? loadedData = _helper.Data.ReadSaveData<EquipmentSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created equipment supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded equipment supply data with {_activeData.EquipmentSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load equipment supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            EquipmentSupplySaveData data = EnsureActiveData();
            if (data.EquipmentSupplyScores.Count == 0)
                return;

            data.EquipmentSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked equipment supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.EquipmentSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.EquipmentSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            EquipmentSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(pair.Key, out string normalizedEquipmentItemId))
                    continue;

                if (!EquipmentEconomyItemRules.IsEquipmentItemId(normalizedEquipmentItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedEquipmentItemId] = EquipmentMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.EquipmentSupplyScores, normalizedScores))
                return false;

            data.EquipmentSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? equipmentItemId)
        {
            if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.EquipmentSupplyScores.TryGetValue(normalizedEquipmentItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string equipmentItemId, float amount, string equipmentDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(equipmentItemId);

            if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(equipmentItemId, out string normalizedEquipmentItemId))
                return 0f;

            if (!EquipmentEconomyItemRules.IsEquipmentItemId(normalizedEquipmentItemId))
                return 0f;

            EquipmentSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedEquipmentItemId);
            float updatedScore = EquipmentMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.EquipmentSupplyScores[normalizedEquipmentItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Equipment supply increased for {FormatEquipmentLabel(equipmentDisplayName, normalizedEquipmentItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static EquipmentSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static EquipmentSupplySaveData CreateNewData()
        {
            return new EquipmentSupplySaveData
            {
                EquipmentSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static EquipmentSupplySaveData NormalizeLoadedData(EquipmentSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            EquipmentSupplySaveData normalizedData = new()
            {
                EquipmentSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.EquipmentSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.EquipmentSupplyScores)
                {
                    if (!EquipmentEconomyItemRules.TryNormalizeEquipmentItemId(pair.Key, out string normalizedEquipmentItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!EquipmentEconomyItemRules.IsEquipmentItemId(normalizedEquipmentItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = EquipmentMarketTuning.ClampSupply(pair.Value);
                    normalizedData.EquipmentSupplyScores[normalizedEquipmentItemId] = clampedScore;
                    if (!string.Equals(normalizedEquipmentItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing equipment supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= EquipmentMarketTuning.MinSupplyScore;
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

        private static string FormatEquipmentLabel(string equipmentDisplayName, string equipmentItemId)
        {
            return string.IsNullOrWhiteSpace(equipmentDisplayName)
                ? equipmentItemId
                : $"{equipmentDisplayName} ({equipmentItemId})";
        }
    }
}
