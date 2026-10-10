namespace HireSkilledHelpers.Workers;

internal static class WorkerExplorationAreaCatalog
{
    public const string Mines = "Mines";
    public const string SkullCavern = "SkullCavern";
    public const string Volcano = "Volcano";
    private static readonly string[] Areas = { Mines, SkullCavern, Volcano };
    public static IReadOnlyList<string> GetAreas() => Areas;
    public static bool IsValid(string? area) => area is Mines or SkullCavern or Volcano;
    public static string GetLabel(string? area) => area switch
    {
        Mines => "Mines", SkullCavern => "Skull Cavern", Volcano => "Ginger Island Volcano", _ => "Unknown area",
    };
    // Exploration never creates or enters a generated dungeon floor.
    public static string Entrance(string area) => area switch
    {
        Mines => "Mine", SkullCavern => "SkullCave", Volcano => "IslandNorth",
        _ => throw new ArgumentOutOfRangeException(nameof(area)),
    };
}
