using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Centralized helpers for active festival checks in Stardew 1.6.
    /// </summary>
    public static class FestivalUtils
    {
        public static bool IsFestivalDay(int dayOfMonth, Season season)
        {
            return Utility.isFestivalDay(dayOfMonth, season);
        }

        public static string? GetFestivalName(int dayOfMonth, Season season)
        {
            if (!IsFestivalDay(dayOfMonth, season))
                return null;

            string festivalId = $"{Utility.getSeasonKey(season)}{dayOfMonth}";
            
            if (!Event.tryToLoadFestivalData(festivalId, out var _, out var data, out var _, out var _, out var _))
                return null;

            if (!data.TryGetValue("name", out string? festivalName) || string.IsNullOrWhiteSpace(festivalName))
                return null;

            return festivalName;
        }

        public static bool IsFestivalTomorrow()
        {
            if (Game1.dayOfMonth >= 28)
                return false;

            return IsFestivalDay(Game1.dayOfMonth + 1, Game1.season);
        }

        public static string? GetFestivalTomorrowName()
        {
            if (!IsFestivalTomorrow())
                return null;

            return GetFestivalName(Game1.dayOfMonth + 1, Game1.season);
        }
    }
}
