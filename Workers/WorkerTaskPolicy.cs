namespace FarmingCapitalist.Workers;

/// <summary>Pure task decisions, separated from game objects for inexpensive regression checks.</summary>
internal static class WorkerTaskPolicy
{
    public const int WorkDayEndsAt = 2200;

    public static bool IsWithinWorkHours(int timeOfDay) => timeOfDay >= 600 && timeOfDay < WorkDayEndsAt;

    public static WorkerTaskKind SelectCropAction(WorkerTaskKind assignment, bool alive, bool harvestable, bool needsWater)
    {
        if (!alive)
        {
            return WorkerTaskKind.Idle;
        }

        if (harvestable && assignment is WorkerTaskKind.HarvestCrops or WorkerTaskKind.TendCrops)
        {
            return WorkerTaskKind.HarvestCrops;
        }

        if (needsWater && assignment is WorkerTaskKind.WaterCrops or WorkerTaskKind.TendCrops)
        {
            return WorkerTaskKind.WaterCrops;
        }

        return WorkerTaskKind.Idle;
    }

    public static string GetTaskLabel(WorkerTaskKind task) => task switch
    {
        WorkerTaskKind.WaterCrops => "Water crops",
        WorkerTaskKind.HarvestCrops => "Harvest crops",
        WorkerTaskKind.TendCrops => "Tend crops",
        _ => "Idle",
    };
}
