namespace FarmingCapitalist.Workers;

/// <summary>Persistent worker assignments. Numeric values must remain stable in existing saves.</summary>
internal enum WorkerTaskKind
{
    Idle = 0,
    WaterCrops = 1,
    HarvestCrops = 2,
    TendCrops = 3,
    CollectForage = 4,
    ChopTrees = 5,
    ChopHardwood = 6,
    ClearDebris = 7,
    SlayMonsters = 8,
    ExploreArea = 9,
    MineRocks = 10,
    MineOreGems = 11,
    FindLadder = 12,
}
