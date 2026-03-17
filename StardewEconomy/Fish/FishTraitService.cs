using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Central resolver for broad fish availability and source traits.
    /// Mirrors the crop trait service pattern while keeping fish parsing fish-specific.
    /// </summary>
    internal static class FishTraitService
    {
        internal static IMonitor? Monitor;

        private const int MorningStartTime = 600;
        private const int DayStartTime = 1200;
        private const int EveningStartTime = 1700;
        private const int NightStartTime = 2000;
        private const int EndOfDayTime = 2600;

        private static readonly string[] AllSeasonKeys =
        {
            "spring",
            "summer",
            "fall",
            "winter"
        };

        public static FishEconomicTrait GetTraits(Item? item)
        {
            if (!TryResolveSourceFishItemId(item, out string sourceFishItemId))
                return FishEconomicTrait.None;

            return GetTraitsForSourceFish(sourceFishItemId);
        }

        public static FishEconomicTrait GetTraits(string? fishItemId)
        {
            if (!TryResolveSourceFishItemId(fishItemId, out string sourceFishItemId))
                return FishEconomicTrait.None;

            return GetTraitsForSourceFish(sourceFishItemId);
        }

        public static bool HasTrait(Item? item, FishEconomicTrait trait)
        {
            if (trait == FishEconomicTrait.None)
                return false;

            FishEconomicTrait traits = GetTraits(item);
            return (traits & trait) == trait;
        }

        public static bool HasTrait(string? fishItemId, FishEconomicTrait trait)
        {
            if (trait == FishEconomicTrait.None)
                return false;

            FishEconomicTrait traits = GetTraits(fishItemId);
            return (traits & trait) == trait;
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(string? fishItemId)
        {
            if (!TryResolveSourceFishItemId(fishItemId, out string sourceFishItemId))
                return Array.Empty<string>();

            return GetAvailableSeasonKeysForSourceFish(sourceFishItemId);
        }

        public static IReadOnlyCollection<string> GetAvailableSeasonKeys(Item? item)
        {
            if (!TryResolveSourceFishItemId(item, out string sourceFishItemId))
                return Array.Empty<string>();

            return GetAvailableSeasonKeysForSourceFish(sourceFishItemId);
        }

        public static bool IsAvailableInSeason(string? fishItemId, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(fishItemId)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAvailableInSeason(Item? item, string? seasonKey)
        {
            if (string.IsNullOrWhiteSpace(seasonKey))
                return false;

            return GetAvailableSeasonKeys(item)
                .Contains(seasonKey.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAvailableAtTime(Item? item, int timeOfDay)
        {
            if (!TryResolveSourceFishItemId(item, out string sourceFishItemId))
                return false;

            if (!TryGetFishDataFields(sourceFishItemId, out string[] fields))
                return false;

            if (IsTrapFish(fields))
                return true;

            if (!TryGetTimeSpans(fields, out IReadOnlyList<(int StartTime, int EndTime)> timeSpans))
                return false;

            foreach ((int startTime, int endTime) in timeSpans)
            {
                if (timeOfDay >= startTime && timeOfDay < endTime)
                    return true;
            }

            return false;
        }

        public static bool IsAvailableAtTime(string? fishItemId, int timeOfDay)
        {
            if (!TryResolveSourceFishItemId(fishItemId, out string sourceFishItemId))
                return false;

            if (!TryGetFishDataFields(sourceFishItemId, out string[] fields))
                return false;

            if (IsTrapFish(fields))
                return true;

            if (!TryGetTimeSpans(fields, out IReadOnlyList<(int StartTime, int EndTime)> timeSpans))
                return false;

            foreach ((int startTime, int endTime) in timeSpans)
            {
                if (timeOfDay >= startTime && timeOfDay < endTime)
                    return true;
            }

            return false;
        }

        public static bool IsSpringFish(Item? item) => HasTrait(item, FishEconomicTrait.Spring);
        public static bool IsSpringFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Spring);
        public static bool IsSummerFish(Item? item) => HasTrait(item, FishEconomicTrait.Summer);
        public static bool IsSummerFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Summer);
        public static bool IsFallFish(Item? item) => HasTrait(item, FishEconomicTrait.Fall);
        public static bool IsFallFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Fall);
        public static bool IsWinterFish(Item? item) => HasTrait(item, FishEconomicTrait.Winter);
        public static bool IsWinterFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Winter);
        public static bool IsMorningFish(Item? item) => HasTrait(item, FishEconomicTrait.Morning);
        public static bool IsMorningFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Morning);
        public static bool IsDayFish(Item? item) => HasTrait(item, FishEconomicTrait.Day);
        public static bool IsDayFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Day);
        public static bool IsEveningFish(Item? item) => HasTrait(item, FishEconomicTrait.Evening);
        public static bool IsEveningFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Evening);
        public static bool IsNightFish(Item? item) => HasTrait(item, FishEconomicTrait.Night);
        public static bool IsNightFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Night);
        public static bool IsRainFish(Item? item) => HasTrait(item, FishEconomicTrait.Rainy);
        public static bool IsRainFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Rainy);
        public static bool IsSunnyFish(Item? item) => HasTrait(item, FishEconomicTrait.Sunny);
        public static bool IsSunnyFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Sunny);
        public static bool IsTrapFish(Item? item) => HasTrait(item, FishEconomicTrait.Trap);
        public static bool IsTrapFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.Trap);
        public static bool IsLineCaughtFish(Item? item) => HasTrait(item, FishEconomicTrait.LineCaught);
        public static bool IsLineCaughtFish(string? fishItemId) => HasTrait(fishItemId, FishEconomicTrait.LineCaught);

        public static bool IsNightOnlyFish(Item? item)
        {
            FishEconomicTrait traits = GetTraits(item);
            return (traits & FishEconomicTrait.Night) == FishEconomicTrait.Night
                && (traits & (FishEconomicTrait.Morning | FishEconomicTrait.Day | FishEconomicTrait.Evening)) == 0;
        }

        public static bool IsNightOnlyFish(string? fishItemId)
        {
            FishEconomicTrait traits = GetTraits(fishItemId);
            return (traits & FishEconomicTrait.Night) == FishEconomicTrait.Night
                && (traits & (FishEconomicTrait.Morning | FishEconomicTrait.Day | FishEconomicTrait.Evening)) == 0;
        }

        public static string FormatTraits(FishEconomicTrait traits)
        {
            if (traits == FishEconomicTrait.None)
                return "None";

            List<string> names = new();
            foreach (FishEconomicTrait trait in Enum.GetValues<FishEconomicTrait>())
            {
                if (trait == FishEconomicTrait.None)
                    continue;

                if ((traits & trait) == trait)
                    names.Add(trait.ToString());
            }

            return string.Join(", ", names);
        }

        public static string GetDebugSummary(Item? item)
        {
            if (item is null)
                return "Fish traits: <null item> -> None";

            FishEconomicTrait traits = GetTraits(item);
            return $"Fish traits: {item.Name} ({item.QualifiedItemId}) -> {FormatTraits(traits)}";
        }

        public static void LogTraits(Item? item, LogLevel level = LogLevel.Trace)
        {
            Monitor?.Log(GetDebugSummary(item), level);
        }

        private static FishEconomicTrait GetTraitsForSourceFish(string sourceFishItemId)
        {
            if (!TryGetFishDataFields(sourceFishItemId, out string[] fields))
                return FishEconomicTrait.None;

            FishEconomicTrait traits = FishEconomicTrait.None;
            if (IsTrapFish(fields))
            {
                traits |= FishEconomicTrait.Trap;
                traits |= FishEconomicTrait.Spring | FishEconomicTrait.Summer | FishEconomicTrait.Fall | FishEconomicTrait.Winter;
                traits |= FishEconomicTrait.Morning | FishEconomicTrait.Day | FishEconomicTrait.Evening | FishEconomicTrait.Night;
                traits |= FishEconomicTrait.Sunny | FishEconomicTrait.Rainy;
                return traits;
            }

            traits |= FishEconomicTrait.LineCaught;
            traits |= GetSeasonTraits(fields);
            traits |= GetTimeTraits(fields);
            traits |= GetWeatherTraits(fields);

            return traits;
        }

        private static IReadOnlyCollection<string> GetAvailableSeasonKeysForSourceFish(string sourceFishItemId)
        {
            if (!TryGetFishDataFields(sourceFishItemId, out string[] fields))
                return AllSeasonKeys;

            if (IsTrapFish(fields))
                return AllSeasonKeys;

            if (!TryGetField(fields, 6, out string rawSeasonList) || string.IsNullOrWhiteSpace(rawSeasonList))
                return AllSeasonKeys;

            List<string> seasonKeys = rawSeasonList
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(season => season.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return seasonKeys.Count > 0
                ? seasonKeys
                : AllSeasonKeys;
        }

        private static FishEconomicTrait GetSeasonTraits(string[] fields)
        {
            IReadOnlyCollection<string> seasonKeys = GetAvailableSeasonKeysForFields(fields);
            FishEconomicTrait traits = FishEconomicTrait.None;

            foreach (string seasonKey in seasonKeys)
            {
                traits |= seasonKey switch
                {
                    "spring" => FishEconomicTrait.Spring,
                    "summer" => FishEconomicTrait.Summer,
                    "fall" => FishEconomicTrait.Fall,
                    "winter" => FishEconomicTrait.Winter,
                    _ => FishEconomicTrait.None
                };
            }

            return traits;
        }

        private static IReadOnlyCollection<string> GetAvailableSeasonKeysForFields(string[] fields)
        {
            if (IsTrapFish(fields))
                return AllSeasonKeys;

            if (!TryGetField(fields, 6, out string rawSeasonList) || string.IsNullOrWhiteSpace(rawSeasonList))
                return AllSeasonKeys;

            List<string> seasonKeys = rawSeasonList
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(season => season.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return seasonKeys.Count > 0
                ? seasonKeys
                : AllSeasonKeys;
        }

        private static FishEconomicTrait GetTimeTraits(string[] fields)
        {
            if (IsTrapFish(fields))
            {
                return FishEconomicTrait.Morning
                    | FishEconomicTrait.Day
                    | FishEconomicTrait.Evening
                    | FishEconomicTrait.Night;
            }

            if (!TryGetTimeSpans(fields, out IReadOnlyList<(int StartTime, int EndTime)> timeSpans))
                return FishEconomicTrait.None;

            FishEconomicTrait traits = FishEconomicTrait.None;
            foreach ((int startTime, int endTime) in timeSpans)
            {
                if (RangesOverlap(startTime, endTime, MorningStartTime, DayStartTime))
                    traits |= FishEconomicTrait.Morning;

                if (RangesOverlap(startTime, endTime, DayStartTime, EveningStartTime))
                    traits |= FishEconomicTrait.Day;

                if (RangesOverlap(startTime, endTime, EveningStartTime, NightStartTime))
                    traits |= FishEconomicTrait.Evening;

                if (RangesOverlap(startTime, endTime, NightStartTime, EndOfDayTime))
                    traits |= FishEconomicTrait.Night;
            }

            return traits;
        }

        private static FishEconomicTrait GetWeatherTraits(string[] fields)
        {
            if (IsTrapFish(fields))
                return FishEconomicTrait.Sunny | FishEconomicTrait.Rainy;

            if (!TryGetField(fields, 7, out string weather))
                return FishEconomicTrait.None;

            return weather.Trim().ToLowerInvariant() switch
            {
                "rainy" => FishEconomicTrait.Rainy,
                "sunny" => FishEconomicTrait.Sunny,
                _ => FishEconomicTrait.Sunny | FishEconomicTrait.Rainy
            };
        }

        private static bool TryResolveSourceFishItemId(Item? item, out string sourceFishItemId)
        {
            sourceFishItemId = string.Empty;

            if (item is not SObject obj)
                return false;

            FishEconomyClassification classification = FishEconomyItemRules.GetFishEconomyClassification(obj);
            if (classification == FishEconomyClassification.None)
                return false;

            if (FishEconomyItemRules.TryGetFishPreserveSourceItemId(obj, out sourceFishItemId))
                return true;

            return FishSupplyTracker.TryNormalizeFishItemId(obj.ItemId, out sourceFishItemId);
        }

        private static bool TryResolveSourceFishItemId(string? fishItemId, out string sourceFishItemId)
        {
            sourceFishItemId = string.Empty;

            if (FishSupplyTracker.TryGetSourceFishItemId(fishItemId, out sourceFishItemId))
                return true;

            if (!FishSupplyTracker.TryNormalizeFishItemId(fishItemId, out string normalizedFishItemId))
                return false;

            if (!TryGetFishDataFields(normalizedFishItemId, out _))
                return false;

            sourceFishItemId = normalizedFishItemId;
            return true;
        }

        private static bool TryGetFishDataFields(string sourceFishItemId, out string[] fields)
        {
            fields = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(sourceFishItemId))
                return false;

            Dictionary<string, string> fishData = DataLoader.Fish(Game1.content);
            if (!fishData.TryGetValue(sourceFishItemId, out string? rawFishData) || string.IsNullOrWhiteSpace(rawFishData))
                return false;

            fields = rawFishData.Split('/');
            return fields.Length > 0;
        }

        private static bool IsTrapFish(string[] fields)
        {
            return TryGetField(fields, 1, out string fieldValue)
                && string.Equals(fieldValue, "trap", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetTimeSpans(string[] fields, out IReadOnlyList<(int StartTime, int EndTime)> timeSpans)
        {
            timeSpans = Array.Empty<(int StartTime, int EndTime)>();

            if (!TryGetField(fields, 5, out string rawTimeSpans) || string.IsNullOrWhiteSpace(rawTimeSpans))
                return false;

            string[] values = rawTimeSpans.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length < 2 || values.Length % 2 != 0)
                return false;

            List<(int StartTime, int EndTime)> parsedTimeSpans = new();
            for (int i = 0; i < values.Length; i += 2)
            {
                if (!int.TryParse(values[i], out int startTime) || !int.TryParse(values[i + 1], out int endTime))
                    return false;

                if (endTime <= startTime)
                    continue;

                parsedTimeSpans.Add((startTime, endTime));
            }

            if (parsedTimeSpans.Count == 0)
                return false;

            timeSpans = parsedTimeSpans;
            return true;
        }

        private static bool TryGetField(string[] fields, int index, out string value)
        {
            value = string.Empty;
            if (fields is null || index < 0 || index >= fields.Length)
                return false;

            value = fields[index];
            return true;
        }

        private static bool RangesOverlap(int leftStart, int leftEnd, int rightStart, int rightEnd)
        {
            return leftStart < rightEnd && rightStart < leftEnd;
        }
    }
}
