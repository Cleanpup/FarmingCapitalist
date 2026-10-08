namespace FarmingCapitalist.Workers;

internal static class WorkerCombatAreaCatalog
{
    public const string Farm = "Farm";
    public const string Mines = "Mines";
    public const string SkullCavern = "SkullCavern";
    public const string IslandFarm = "IslandWest";
    public const string VolcanoEntrance = "IslandNorth";

    private static readonly string[] Areas = { Farm, Mines, SkullCavern, IslandFarm, VolcanoEntrance };

    public static IReadOnlyList<string> GetAreas() => Areas;

    public static bool IsValid(string? area) => area is Farm or Mines or SkullCavern or IslandFarm or VolcanoEntrance;

    public static bool IsDungeon(string? area) => area is Mines or SkullCavern;

    public static string GetLabel(string? area) => area switch
    {
        Farm => "Farm",
        Mines => "Mines",
        SkullCavern => "Skull Cavern",
        IslandFarm => "Ginger Island farm",
        VolcanoEntrance => "Near Volcano entrance",
        _ => "Unknown area",
    };
}
