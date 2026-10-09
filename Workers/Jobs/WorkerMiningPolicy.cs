namespace FarmingCapitalist.Workers;

/// <summary>Initial physical mining works only in permanent outdoor resource areas.</summary>
internal static class WorkerMiningPolicy
{
    public const string Farm = "Farm";
    public const string Quarry = "Quarry";
    public const int PickaxeUpgradeLevel = 2;
    public const int PickaxeDamage = PickaxeUpgradeLevel + 1;

    public static readonly IReadOnlyList<string> Areas = Array.AsReadOnly(new[] { Farm, Quarry });

    public static bool IsValid(string? area) => area is Farm or Quarry;
    public static string GetLocationName(string area) => area == Quarry ? "Mountain" : Farm;

    // Mountain.quarryDayUpdate's authored spawn rectangle. Avoid other Mountain objects.
    public static bool ContainsTile(string area, int x, int y)
        => area == Farm || (area == Quarry && x >= 106 && x < 128 && y >= 13 && y < 35);
}
