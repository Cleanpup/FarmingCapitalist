using System;
using StardewValley;
using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Immutable snapshot of economy-relevant state when a shop opens.
    /// </summary>
    internal class EconomyContext
    {
        public string Season { get; init; } = string.Empty;
        public int DayOfMonth { get; init; }
        public bool IsFestivalToday { get; init; }
        public bool FestivalTomorrow { get; init; }
        public string? FestivalTomorrowName { get; init; }
        public int FarmingLevel { get; init; }
        public int FishingLevel { get; init; }
        public int MiningLevel { get; init; }
        public int HeartsWithShopkeeper { get; init; }
    }

    /// <summary>
    /// Helper to collect current game state for economy calculations.
    /// </summary>
    internal static class EconomyContextBuilder
    {
        internal static IMonitor? Monitor;
        public static EconomyContext Build(string? shopkeeperName, IMonitor? monitor = null)
        {
            Farmer? player = Game1.player;

            int hearts = 0;
            Monitor?.Log($"Building EconomyContext for shopkeeper: {shopkeeperName}", LogLevel.Trace);
            if (!string.IsNullOrWhiteSpace(shopkeeperName))
            {
                try
                {
                    hearts = player?.getFriendshipHeartLevelForNPC(shopkeeperName) ?? 0;
                    (monitor ?? Monitor)?.Log($"Hearts with {shopkeeperName}: {hearts}", LogLevel.Trace);
                }
                catch
                {
                    hearts = 0;
                    (monitor ?? Monitor)?.Log($"Failed to retrieve friendship hearts for {shopkeeperName}.", LogLevel.Trace);
                }
            }

            int tomorrowDay = Game1.dayOfMonth + 1;
            Season tomorrowSeason = Game1.season;
            const int daysPerSeason = 28;

            if (tomorrowDay > daysPerSeason)
            {
                tomorrowDay = 1;
                tomorrowSeason = (Season)(((int)tomorrowSeason + 1) % 4);
            }

            bool festivalTomorrow = Utility.isFestivalDay(tomorrowDay, tomorrowSeason);
            string? festivalTomorrowName = null;

            if (festivalTomorrow)
            {
                festivalTomorrowName = TryGetFestivalName(tomorrowDay, tomorrowSeason, monitor ?? Monitor);
            }

            return new EconomyContext
            {
                Season = Game1.currentSeason ?? string.Empty,
                DayOfMonth = Game1.dayOfMonth,
                IsFestivalToday = Game1.isFestival(),
                FestivalTomorrow = festivalTomorrow,
                FestivalTomorrowName = festivalTomorrowName,
                FarmingLevel = player?.FarmingLevel ?? 0,
                FishingLevel = player?.FishingLevel ?? 0,
                MiningLevel = player?.MiningLevel ?? 0,
                HeartsWithShopkeeper = hearts
            };
        }

        private static string? TryGetFestivalName(int day, Season season, IMonitor? monitor)
        {
            try
            {
                var getFestivalName = typeof(Utility).GetMethod("getFestivalName", new[] { typeof(int), typeof(Season) });
                if (getFestivalName != null)
                {
                    return getFestivalName.Invoke(null, new object[] { day, season }) as string;
                }

                monitor?.Log("Utility.getFestivalName not found; festival name will be omitted.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                monitor?.Log($"Failed to retrieve festival name for day {day} of {season}: {ex.Message}", LogLevel.Trace);
            }

            return null;
        }
    }
}
