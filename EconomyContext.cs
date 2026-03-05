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

            bool festivalTomorrow = FestivalUtils.IsFestivalTomorrow();
            string? festivalTomorrowName = FestivalUtils.GetFestivalTomorrowName();

            (monitor ?? Monitor)?.Log(
                $"Festival tomorrow check: {festivalTomorrow} (name: {festivalTomorrowName ?? "none"})",
                LogLevel.Trace
            );

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
    }
}
