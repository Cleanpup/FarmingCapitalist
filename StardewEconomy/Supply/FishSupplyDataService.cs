using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Handles save-data persistence and runtime mutation for fish supply scores.
    /// This stays separate from crop supply persistence for now.
    /// </summary>
    internal static class FishSupplyDataService
    {
        private const string SaveDataKey = "fish-supply";
        internal const float NeutralSupplyScore = 100f;
        private const float DailyDecayFactor = 0.79f;
        private const float NeutralSnapThreshold = 0.01f;

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
                        $"Created fish supply data for this save. LastDecayDay={_activeData.LastDecayDay}.",
                        LogLevel.Trace
                    );
                    return;
                }

                _activeData = NormalizeLoadedData(loadedData, out bool shouldPersist);
                if (shouldPersist)
                    TryWriteActiveData();

                _monitor?.Log(
                    $"Loaded fish supply data with {_activeData.FishSupplyScores.Count} tracked fish. LastDecayDay={_activeData.LastDecayDay}.",
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

                normalizedScores[normalizedFishItemId] = pair.Value;
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

            float updatedScore = previousScore + amount;
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

        public static bool ApplyDailyDecayIfNeeded()
        {
            if (!Context.IsWorldReady)
                return false;

            if (FishSupplyModifierService.HasDebugSellModifierOverride)
                return false;

            FishSupplySaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            if (data.LastDecayDay < 0)
            {
                data.LastDecayDay = currentDay;
                TryWriteActiveData();
                return false;
            }

            int elapsedDays = currentDay - data.LastDecayDay;
            if (elapsedDays <= 0)
                return false;

            return ApplyDecayInternal(data, elapsedDays, currentDay, "day-start");
        }

        public static bool ApplyDebugDecay(int elapsedDays)
        {
            if (!Context.IsWorldReady || elapsedDays <= 0)
                return false;

            FishSupplySaveData data = EnsureActiveData();
            int currentDay = GetCurrentDayKey();
            if (currentDay < 0)
                return false;

            return ApplyDecayInternal(data, elapsedDays, currentDay, "debug-command");
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
                FishSupplyScores = new Dictionary<string, float>(KeyComparer),
                LastDecayDay = GetCurrentDayKey()
            };
        }

        private static FishSupplySaveData NormalizeLoadedData(FishSupplySaveData loadedData, out bool shouldPersist)
        {
            shouldPersist = false;

            FishSupplySaveData normalizedData = new()
            {
                FishSupplyScores = new Dictionary<string, float>(KeyComparer),
                LastDecayDay = loadedData.LastDecayDay
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

                    normalizedData.FishSupplyScores[normalizedFishItemId] = pair.Value;
                    if (!string.Equals(normalizedFishItemId, pair.Key, StringComparison.OrdinalIgnoreCase))
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

        private static bool ApplyDecayInternal(FishSupplySaveData data, int elapsedDays, int currentDay, string source)
        {
            if (elapsedDays <= 0)
                return false;

            if (data.FishSupplyScores.Count == 0)
            {
                data.LastDecayDay = currentDay;
                TryWriteActiveData();

                _monitor?.Log(
                    $"Fish supply decay checked for {elapsedDays} day(s) from {source}; no tracked fish required updates.",
                    LogLevel.Trace
                );
                return false;
            }

            float deviationMultiplier = MathF.Pow(DailyDecayFactor, elapsedDays);
            bool changed = false;
            foreach (string fishKey in data.FishSupplyScores.Keys.ToList())
            {
                float previousScore = data.FishSupplyScores[fishKey];
                float updatedScore = CalculateDecayedScore(previousScore, deviationMultiplier);
                data.FishSupplyScores[fishKey] = updatedScore;

                if (updatedScore != previousScore)
                    changed = true;
            }

            data.LastDecayDay = currentDay;
            TryWriteActiveData();

            if (changed)
            {
                _monitor?.Log(
                    $"Applied fish supply decay toward neutral for {elapsedDays} day(s) from {source} at x{DailyDecayFactor:0.###}/day. Tracked fish: {data.FishSupplyScores.Count}. Neutral baseline remains {NeutralSupplyScore:0.##}.",
                    LogLevel.Info
                );
            }
            else
            {
                _monitor?.Log(
                    $"Fish supply decay checked for {elapsedDays} day(s) from {source}; tracked fish were already at the neutral baseline.",
                    LogLevel.Trace
                );
            }

            return changed;
        }

        private static float CalculateDecayedScore(float currentScore, float deviationMultiplier)
        {
            float deviationFromNeutral = currentScore - NeutralSupplyScore;
            float decayedDeviation = deviationFromNeutral * deviationMultiplier;
            float updatedScore = NeutralSupplyScore + decayedDeviation;

            if (MathF.Abs(updatedScore - NeutralSupplyScore) <= NeutralSnapThreshold)
                return NeutralSupplyScore;

            return Math.Max(0f, updatedScore);
        }

        private static string FormatFishLabel(string fishDisplayName, string fishItemId)
        {
            return string.IsNullOrWhiteSpace(fishDisplayName)
                ? fishItemId
                : $"{fishDisplayName} ({fishItemId})";
        }
    }
}
