using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for crafting-extra supply scores.
    /// </summary>
    internal static class CraftingExtraSupplyDataService
    {
        private const string SaveDataKey = "crafting-extra-supply";
        internal const float NeutralSupplyScore = CraftingExtraMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static CraftingExtraSupplySaveData? _activeData;

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
                CraftingExtraSupplySaveData? loadedData = _helper.Data.ReadSaveData<CraftingExtraSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created crafting extra supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded crafting extra supply data with {_activeData.CraftingExtraSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load crafting extra supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            CraftingExtraSupplySaveData data = EnsureActiveData();
            if (data.CraftingExtraSupplyScores.Count == 0)
                return;

            data.CraftingExtraSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked crafting extra supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.CraftingExtraSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.CraftingExtraSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            CraftingExtraSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(pair.Key, out string normalizedCraftingExtraItemId))
                    continue;

                if (!CraftingExtraEconomyItemRules.IsCraftingExtraItemId(normalizedCraftingExtraItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedCraftingExtraItemId] = CraftingExtraMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.CraftingExtraSupplyScores, normalizedScores))
                return false;

            data.CraftingExtraSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? craftingExtraItemId)
        {
            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.CraftingExtraSupplyScores.TryGetValue(normalizedCraftingExtraItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string craftingExtraItemId, float amount, string craftingExtraDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(craftingExtraItemId);

            if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(craftingExtraItemId, out string normalizedCraftingExtraItemId))
                return 0f;

            if (!CraftingExtraEconomyItemRules.IsCraftingExtraItemId(normalizedCraftingExtraItemId))
                return 0f;

            CraftingExtraSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedCraftingExtraItemId);
            float updatedScore = CraftingExtraMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.CraftingExtraSupplyScores[normalizedCraftingExtraItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"CraftingExtra supply increased for {FormatCraftingExtraLabel(craftingExtraDisplayName, normalizedCraftingExtraItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static CraftingExtraSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static CraftingExtraSupplySaveData CreateNewData()
        {
            return new CraftingExtraSupplySaveData
            {
                CraftingExtraSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static CraftingExtraSupplySaveData NormalizeLoadedData(CraftingExtraSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            CraftingExtraSupplySaveData normalizedData = new()
            {
                CraftingExtraSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.CraftingExtraSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.CraftingExtraSupplyScores)
                {
                    if (!CraftingExtraEconomyItemRules.TryNormalizeCraftingExtraItemId(pair.Key, out string normalizedCraftingExtraItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!CraftingExtraEconomyItemRules.IsCraftingExtraItemId(normalizedCraftingExtraItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = CraftingExtraMarketTuning.ClampSupply(pair.Value);
                    normalizedData.CraftingExtraSupplyScores[normalizedCraftingExtraItemId] = clampedScore;
                    if (!string.Equals(normalizedCraftingExtraItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing crafting extra supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= CraftingExtraMarketTuning.MinSupplyScore;
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

        private static string FormatCraftingExtraLabel(string craftingExtraDisplayName, string craftingExtraItemId)
        {
            return string.IsNullOrWhiteSpace(craftingExtraDisplayName)
                ? craftingExtraItemId
                : $"{craftingExtraDisplayName} ({craftingExtraItemId})";
        }
    }
}
