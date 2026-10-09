using Microsoft.Xna.Framework;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal static class WorkerFishingAreaCatalog
{
    public const string Forest = "Forest", Mountain = "Mountain", Town = "Town", Beach = "Beach", Island = "IslandSouth";
    public static readonly IReadOnlyList<string> Areas = new[] { Forest, Mountain, Town, Beach, Island };
    public static bool IsValid(string area) => Areas.Contains(area);
    public static string Label(string area) => area switch
    {
        Forest => "Cindersap Forest", Mountain => "Mountain", Town => "Town", Beach => "Beach",
        Island => "Ginger Island beach", _ => "Unknown area",
    };
    public static Point Anchor(string area) => area switch
    {
        Forest => new(66, 50), Mountain => new(49, 20), Town => new(69, 81), Beach => new(35, 36),
        Island => new(19, 35), _ => Point.Zero,
    };
    public static string? AccessReason(string area) => !IsValid(area) ? "Unknown fishing area"
        : area == Island && !Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed")
            ? "Repair Willy's boat before fishing on Ginger Island" : null;
}
