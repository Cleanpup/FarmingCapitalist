namespace FarmingCapitalist.Workers;

/// <summary>Pure task decisions, separated from game objects for inexpensive regression checks.</summary>
internal static class WorkerTaskPolicy
{
    private static readonly WorkerTaskKind[] FarmerTasks =
    {
        WorkerTaskKind.WaterCrops, WorkerTaskKind.HarvestCrops, WorkerTaskKind.TendCrops,
        WorkerTaskKind.ClearDebris, WorkerTaskKind.Idle,
    };

    private static readonly WorkerTaskKind[] ForagerTasks =
    {
        WorkerTaskKind.ChopTrees, WorkerTaskKind.CollectForage, WorkerTaskKind.ChopHardwood,
        WorkerTaskKind.ClearDebris, WorkerTaskKind.Idle,
    };

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

    public static IReadOnlyList<WorkerTaskKind> GetTasks(WorkerProfession profession)
        => profession == WorkerProfession.Forager ? ForagerTasks : FarmerTasks;

    public static bool IsTaskAllowed(WorkerProfession profession, WorkerTaskKind task)
    {
        foreach (WorkerTaskKind allowed in GetTasks(profession))
        {
            if (allowed == task)
                return true;
        }

        return false;
    }

    public static string GetProfessionLabel(WorkerProfession profession)
        => profession == WorkerProfession.Forager ? "Forager" : "Farmer";

    public static string GetTaskLabel(WorkerTaskKind task, string idleLabel = "Idle") => task switch
    {
        WorkerTaskKind.WaterCrops => "Water crops",
        WorkerTaskKind.HarvestCrops => "Harvest crops",
        WorkerTaskKind.TendCrops => "Tend crops",
        WorkerTaskKind.CollectForage => "Collect forage",
        WorkerTaskKind.ChopTrees => "Cut down trees",
        WorkerTaskKind.ChopHardwood => "Cut hardwood",
        WorkerTaskKind.ClearDebris => "Clear debris",
        _ => idleLabel,
    };
}
