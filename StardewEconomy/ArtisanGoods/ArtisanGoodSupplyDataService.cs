using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for artisan-good supply scores.
    /// </summary>
    internal static class ArtisanGoodSupplyDataService
    {
        private const string SaveDataKey = "artisan-good-supply";
        internal const float NeutralSupplyScore = ArtisanGoodMarketTuning.NeutralSupplyScore;

        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private static IModHelper? _helper;
        private static IMonitor? _monitor;
        private static ArtisanGoodSupplySaveData? _activeData;

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
                ArtisanGoodSupplySaveData? loadedData = _helper.Data.ReadSaveData<ArtisanGoodSupplySaveData>(SaveDataKey);
                if (loadedData is null)
                {
                    _activeData = CreateNewData();
                    TryWriteActiveData();

                    _monitor?.Log("Created artisan good supply data for this save.", LogLevel.Trace);
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded artisan good supply data with {_activeData.ArtisanGoodSupplyScores.Count} tracked items.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Failed to load artisan good supply data: {ex}", LogLevel.Error);
                _activeData = CreateNewData();
            }
        }

        public static void ClearActiveData()
        {
            _activeData = null;
        }

        public static void ResetTrackedSupply()
        {
            ArtisanGoodSupplySaveData data = EnsureActiveData();
            if (data.ArtisanGoodSupplyScores.Count == 0)
                return;

            data.ArtisanGoodSupplyScores.Clear();
            TryWriteActiveData();

            _monitor?.Log("Cleared all tracked artisan good supply scores and restored the neutral supply baseline.", LogLevel.Info);
        }

        public static IReadOnlyDictionary<string, float> GetSnapshot()
        {
            if (_activeData is null || _activeData.ArtisanGoodSupplyScores.Count == 0)
                return new Dictionary<string, float>(KeyComparer);

            return new Dictionary<string, float>(_activeData.ArtisanGoodSupplyScores, KeyComparer);
        }

        public static bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores)
        {
            ArtisanGoodSupplySaveData data = EnsureActiveData();
            Dictionary<string, float> normalizedScores = new(KeyComparer);

            foreach (KeyValuePair<string, float> pair in supplyScores)
            {
                if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(pair.Key, out string normalizedArtisanGoodItemId))
                    continue;

                if (!ArtisanGoodEconomyItemRules.IsArtisanGoodItemId(normalizedArtisanGoodItemId))
                    continue;

                if (!IsValidScore(pair.Value))
                    continue;

                normalizedScores[normalizedArtisanGoodItemId] = ArtisanGoodMarketTuning.ClampSupply(pair.Value);
            }

            if (HaveSameScores(data.ArtisanGoodSupplyScores, normalizedScores))
                return false;

            data.ArtisanGoodSupplyScores = normalizedScores;
            TryWriteActiveData();
            return true;
        }

        public static float GetSupplyScore(string? artisanGoodItemId)
        {
            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return NeutralSupplyScore;

            if (_activeData is null)
                return NeutralSupplyScore;

            return _activeData.ArtisanGoodSupplyScores.TryGetValue(normalizedArtisanGoodItemId, out float score)
                ? score
                : NeutralSupplyScore;
        }

        public static float AddSupply(string artisanGoodItemId, float amount, string artisanGoodDisplayName, string source)
        {
            if (amount <= 0f)
                return GetSupplyScore(artisanGoodItemId);

            if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(artisanGoodItemId, out string normalizedArtisanGoodItemId))
                return 0f;

            if (!ArtisanGoodEconomyItemRules.IsArtisanGoodItemId(normalizedArtisanGoodItemId))
                return 0f;

            ArtisanGoodSupplySaveData data = EnsureActiveData();
            float previousScore = GetSupplyScore(normalizedArtisanGoodItemId);
            float updatedScore = ArtisanGoodMarketTuning.ClampSupply(previousScore + amount);
            if (updatedScore == previousScore)
                return updatedScore;

            data.ArtisanGoodSupplyScores[normalizedArtisanGoodItemId] = updatedScore;
            TryWriteActiveData();

            _monitor?.Log(
                $"Artisan good supply increased for {FormatArtisanGoodLabel(artisanGoodDisplayName, normalizedArtisanGoodItemId)} by {amount:0.##} from {source}. {previousScore:0.##} -> {updatedScore:0.##}.",
                LogLevel.Trace
            );

            return updatedScore;
        }

        private static ArtisanGoodSupplySaveData EnsureActiveData()
        {
            _activeData ??= CreateNewData();
            return _activeData;
        }

        private static ArtisanGoodSupplySaveData CreateNewData()
        {
            return new ArtisanGoodSupplySaveData
            {
                ArtisanGoodSupplyScores = new Dictionary<string, float>(KeyComparer)
            };
        }

        private static ArtisanGoodSupplySaveData NormalizeLoadedData(ArtisanGoodSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            ArtisanGoodSupplySaveData normalizedData = new()
            {
                ArtisanGoodSupplyScores = new Dictionary<string, float>(KeyComparer)
            };

            if (loadedData.ArtisanGoodSupplyScores is not null)
            {
                foreach (KeyValuePair<string, float> pair in loadedData.ArtisanGoodSupplyScores)
                {
                    if (!ArtisanGoodEconomyItemRules.TryNormalizeArtisanGoodItemId(pair.Key, out string normalizedArtisanGoodItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!ArtisanGoodEconomyItemRules.IsArtisanGoodItemId(normalizedArtisanGoodItemId))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    if (!IsValidScore(pair.Value))
                    {
                        shouldPersist = true;
                        continue;
                    }

                    float clampedScore = ArtisanGoodMarketTuning.ClampSupply(pair.Value);
                    normalizedData.ArtisanGoodSupplyScores[normalizedArtisanGoodItemId] = clampedScore;
                    if (!string.Equals(normalizedArtisanGoodItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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
                _monitor?.Log($"Failed writing artisan good supply data: {ex}", LogLevel.Error);
            }
        }

        private static bool IsValidScore(float value)
        {
            return !float.IsNaN(value)
                && !float.IsInfinity(value)
                && value >= ArtisanGoodMarketTuning.MinSupplyScore;
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

        private static string FormatArtisanGoodLabel(string artisanGoodDisplayName, string artisanGoodItemId)
        {
            return string.IsNullOrWhiteSpace(artisanGoodDisplayName)
                ? artisanGoodItemId
                : $"{artisanGoodDisplayName} ({artisanGoodItemId})";
        }
    }
}
