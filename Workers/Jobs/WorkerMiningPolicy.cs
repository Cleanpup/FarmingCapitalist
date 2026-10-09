namespace FarmingCapitalist.Workers;

internal enum WorkerMiningWorkMode
{
    None,
    Ores,
    AllStone,
    FindLadder,
}

/// <summary>Saved areas, resource selection and progression rules for physical mining.</summary>
internal static class WorkerMiningPolicy
{
    // Farm remains supported for saves from the initial mining implementation.
    public const string Farm = "Farm";
    public const string Quarry = "Quarry";
    public const string Mines = "Mines";
    public const string SkullCavern = "SkullCavern";
    public const string Volcano = "Volcano";
    public const string DefaultArea = Quarry;
    public const int PickaxeUpgradeLevel = 2;
    public const int PickaxeDamage = PickaxeUpgradeLevel + 1;

    public static readonly IReadOnlyList<string> Areas = Array.AsReadOnly(new[] { Quarry, Mines, SkullCavern, Volcano });

    public static bool IsValid(string? area) => area is Farm or Quarry or Mines or SkullCavern or Volcano;
    public static bool IsDungeon(string area) => area is Mines or SkullCavern or Volcano;
    public static bool IsMiningTask(WorkerTaskKind task)
        => task is WorkerTaskKind.MineRocks or WorkerTaskKind.MineOreGems or WorkerTaskKind.FindLadder;
    public static string GetLabel(string area) => area switch
    {
        SkullCavern => "Skull Cavern",
        Volcano => "Ginger Island Volcano",
        _ => area,
    };
    public static string GetLocationName(string area) => area switch
    {
        Quarry => "Mountain",
        Mines => "Mine",
        SkullCavern => "SkullCave",
        Volcano => "IslandNorth",
        _ => Farm,
    };

    // Mountain.quarryDayUpdate's authored spawn rectangle. Avoid other Mountain objects.
    public static bool ContainsTile(string area, int x, int y)
        => IsDungeon(area) || area == Farm || (area == Quarry && x >= 106 && x < 128 && y >= 13 && y < 35);

    // GameLocation.breakStone / VolcanoDungeon.createStone vanilla mineral nodes.
    // Ordinary rocks with a chance of coal remain ordinary stone for selection.
    public static bool IsOreGemOrCoal(string? itemId) => itemId is
        "2" or "4" or "6" or "8" or "10" or "12" or "14" or "44" or "46"
        or "75" or "76" or "77" or "95" or "290" or "751" or "764" or "765"
        or "819" or "843" or "844" or "849" or "850"
        or "BasicCoalNode0" or "BasicCoalNode1" or "VolcanoCoalNode0" or "VolcanoCoalNode1" or "VolcanoGoldNode";

    public static WorkerMiningWorkMode SelectMode(WorkerTaskKind assignment, bool hasResources,
        bool hasMineLadders, bool hasExit, bool monsterGate, bool canCreateLadder)
    {
        if (assignment == WorkerTaskKind.MineRocks) return WorkerMiningWorkMode.AllStone;
        if (assignment == WorkerTaskKind.MineOreGems && hasResources) return WorkerMiningWorkMode.Ores;
        if (assignment is not WorkerTaskKind.MineOreGems and not WorkerTaskKind.FindLadder)
            return WorkerMiningWorkMode.None;
        return hasMineLadders && !hasExit && !monsterGate && canCreateLadder
            ? WorkerMiningWorkMode.FindLadder : WorkerMiningWorkMode.None;
    }

    public static bool CanTarget(WorkerMiningWorkMode mode, string itemId)
        => mode is WorkerMiningWorkMode.AllStone or WorkerMiningWorkMode.FindLadder
            || (mode == WorkerMiningWorkMode.Ores && IsOreGemOrCoal(itemId));

    // These items need the player's pickup handling for progression/readability.
    public static bool LeaveDropForPlayer(string qualifiedItemId)
        => qualifiedItemId is "(O)73" or "(O)79" or "(O)842";
}
