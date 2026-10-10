namespace HireSkilledHelpers.Workers;

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

    private static readonly WorkerTaskKind[] CombatTasks = { WorkerTaskKind.SlayMonsters, WorkerTaskKind.ExploreArea, WorkerTaskKind.Idle };
    private static readonly WorkerTaskKind[] MinerTasks = { WorkerTaskKind.MineOreGems, WorkerTaskKind.FindLadder, WorkerTaskKind.MineRocks, WorkerTaskKind.Idle };

    private static readonly WorkerTaskKind[] FisherTasks = { WorkerTaskKind.Fish, WorkerTaskKind.Idle };

    public const int WorkDayEndsAt = 2200;

    public static bool IsWithinWorkHours(int timeOfDay) => timeOfDay >= 600 && timeOfDay < WorkDayEndsAt;

    /// <summary>Only a completed daytime order becomes Idle; breaks and overnight returns retain their job.</summary>
    public static bool ShouldIdleAfterWorkComplete(WorkerTaskKind assignment, int timeOfDay,
        bool canWorkToday, bool restingForStamina, bool defeatedToday)
        => assignment != WorkerTaskKind.Idle && IsWithinWorkHours(timeOfDay)
            && canWorkToday && !restingForStamina && !defeatedToday;

    public static void AssignTask(WorkerRosterEntry entry, WorkerTaskKind task, int today, bool completedDailyOrder = false)
    {
        entry.NextDayTask = completedDailyOrder && task == WorkerTaskKind.Idle
            && entry.AssignedTask != WorkerTaskKind.Idle ? entry.AssignedTask : null;
        entry.CompletedTaskDay = entry.NextDayTask is not null ? today : -1;
        entry.AssignedTask = task;
    }

    /// <summary>Changing roles cancels orders, but never changes identity, skills, vitals or saved loot.</summary>
    public static bool TryChangeProfession(WorkerRosterEntry entry, WorkerProfession profession, int today,
        bool isHost, bool worldReady)
    {
        if (!isHost || !worldReady || !Enum.IsDefined(typeof(WorkerProfession), profession)) return false;
        if (entry.Profession == profession) return true;
        entry.Profession = profession;
        AssignTask(entry, WorkerTaskKind.Idle, today);
        return true;
    }

    public static void NormalizeCompletedOrder(WorkerRosterEntry entry, int today)
    {
        if (entry.AssignedTask != WorkerTaskKind.Idle || entry.NextDayTask is not WorkerTaskKind task
            || task == WorkerTaskKind.Idle || !IsTaskAllowed(entry.Profession, task)
            || entry.CompletedTaskDay < 0 || entry.CompletedTaskDay > today)
        {
            entry.NextDayTask = null;
            entry.CompletedTaskDay = -1;
        }
    }

    public static bool RestoreDailyOrder(WorkerRosterEntry entry, int today)
    {
        NormalizeCompletedOrder(entry, today);
        if (entry.NextDayTask is not WorkerTaskKind task || today <= entry.CompletedTaskDay)
            return false;
        AssignTask(entry, task, today);
        return true;
    }

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
        => profession switch
        {
            WorkerProfession.Forager => ForagerTasks,
            WorkerProfession.CombatWorker => CombatTasks,
            WorkerProfession.Miner => MinerTasks,
            WorkerProfession.Fisher => FisherTasks,
            _ => FarmerTasks,
        };

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
        => profession switch
        {
            WorkerProfession.Forager => "Forager",
            WorkerProfession.CombatWorker => "Fighter",
            WorkerProfession.Miner => "Miner",
            WorkerProfession.Fisher => "Fisher",
            _ => "Farmer",
        };

    public static string GetTaskLabel(WorkerTaskKind task, string idleLabel = "Idle") => task switch
    {
        WorkerTaskKind.WaterCrops => "Water crops",
        WorkerTaskKind.HarvestCrops => "Harvest crops",
        WorkerTaskKind.TendCrops => "Tend crops",
        WorkerTaskKind.CollectForage => "Collect forage",
        WorkerTaskKind.ChopTrees => "Cut down trees",
        WorkerTaskKind.ChopHardwood => "Cut hardwood",
        WorkerTaskKind.ClearDebris => "Clear debris",
        WorkerTaskKind.SlayMonsters => "Slay monsters",
        WorkerTaskKind.ExploreArea => "Explore Area",
        WorkerTaskKind.MineRocks => "Mine any stone",
        WorkerTaskKind.MineOreGems => "Mine ores/gems",
        WorkerTaskKind.FindLadder => "Find ladder",
        WorkerTaskKind.Fish => "Fish",
        _ => idleLabel,
    };
}
