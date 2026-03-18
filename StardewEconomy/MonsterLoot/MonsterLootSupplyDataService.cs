using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for monster-loot supply scores.
    /// </summary>
    internal static class MonsterLootSupplyDataService
    {
        private const string SaveDataKey = "monster-loot-supply";
        internal const float NeutralSupplyScore = MonsterLootMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static MonsterLootSupplySaveData? _activeData;

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
                MonsterLootSupplySaveData? loadedData = _helper.Data.ReadSaveData<MonsterLootSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created monster loot supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded monster loot supply data with {_activeData.MonsterLootSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load monster loot supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            MonsterLootSupplySaveData data = EnsureActiveData();
            if (data.MonsterLootSupplyScores.Count == 0)
                return;

            data.MonsterLootSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked monster loot supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.MonsterLootSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.MonsterLootSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            MonsterLootSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(pair.Key, out string normalizedMonsterLootItemId))
                    continue;

                if (!MonsterLootEconomyItemRules.IsMonsterLootItemId(normalizedMonsterLootItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedMonsterLootItemId] = MonsterLootMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.MonsterLootSupplyScores, normalizedScores))
                return false;

            data.MonsterLootSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? monsterLootItemId)
        {
            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.MonsterLootSupplyScores.TryGetValue(normalizedMonsterLootItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string monsterLootItemId, float amount, string monsterLootDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(monsterLootItemId);

            if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(monsterLootItemId, out string normalizedMonsterLootItemId))
                return 0f;

            if (!MonsterLootEconomyItemRules.IsMonsterLootItemId(normalizedMonsterLootItemId))
                return 0f;

            MonsterLootSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedMonsterLootItemId);
            float updatedScore = MonsterLootMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.MonsterLootSupplyScores[normalizedMonsterLootItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Monster loot supply increased for {FormatMonsterLootLabel(monsterLootDisplayName, normalizedMonsterLootItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static MonsterLootSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static MonsterLootSupplySaveData CreateNewData()
        {
            return new MonsterLootSupplySaveData
            {
                MonsterLootSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static MonsterLootSupplySaveData NormalizeLoadedData(MonsterLootSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            MonsterLootSupplySaveData normalizedData = new()
            {
                MonsterLootSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.MonsterLootSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.MonsterLootSupplyScores)
                {
                    if (!MonsterLootEconomyItemRules.TryNormalizeMonsterLootItemId(pair.Key, out string normalizedMonsterLootItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!MonsterLootEconomyItemRules.IsMonsterLootItemId(normalizedMonsterLootItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = MonsterLootMarketTuning.ClampSupply(pair.Value);
                    normalizedData.MonsterLootSupplyScores[normalizedMonsterLootItemId] = clampedScore;
                    if (!string.Equals(normalizedMonsterLootItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing monster loot supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= MonsterLootMarketTuning.MinSupplyScore;
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

        private static string FormatMonsterLootLabel(string monsterLootDisplayName, string monsterLootItemId)
        {
            return string.IsNullOrWhiteSpace(monsterLootDisplayName)
                ? monsterLootItemId
                : $"{monsterLootDisplayName} ({monsterLootItemId})";
        }
    }
}
