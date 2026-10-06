namespace FarmingCapitalist.Workers;

/// <summary>Worker skill thresholds and awards, independent of player skills.</summary>
internal static class WorkerExperiencePolicy
{
    // Change this one value to tune experience earned per successful crop harvest.
    public const int FarmingExperiencePerCropHarvest = 10;
    public const int ForagingExperiencePerForage = 7;
    public const int ForagingExperiencePerTree = 12;
    public const int ForagingExperiencePerHardwood = 25;
    public const int ExperiencePerSmallDebris = 1;

    // Farmer.getBaseExperienceForLevel in the installed Stardew Valley 1.6 assembly.
    private static readonly int[] FarmingLevelThresholds = { 0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };

    public const int MaximumLevel = 10;

    public static int GetLevel(int experience)
    {
        int level = 0;
        for (int next = 1; next <= MaximumLevel && experience >= FarmingLevelThresholds[next]; next++)
            level = next;
        return level;
    }

    public static int GetFarmingLevel(int experience) => GetLevel(experience);

    public static int GetThreshold(int level) => FarmingLevelThresholds[Math.Clamp(level, 0, MaximumLevel)];

    public static bool IsConfirmedCropHarvest(bool harvestReturned, bool regrows,
        int regrowDaysBefore, int regrowDaysAfter, int itemsCollected)
        => itemsCollected > 0 && (harvestReturned
            || (regrows && regrowDaysBefore <= 0 && regrowDaysAfter > 0));

    public static bool TryAwardFarmingHarvest(WorkerRosterEntry worker, bool isHost, bool isWorldReady,
        bool confirmedCropChange, int itemsCollected)
    {
        if (!isHost || !isWorldReady || !confirmedCropChange || itemsCollected <= 0)
            return false;

        worker.Experience ??= new WorkerSkillExperience();
        worker.Experience.Farming = (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, worker.Experience.Farming) + FarmingExperiencePerCropHarvest);
        return true;
    }

    public static bool TryAwardCompletedAction(WorkerRosterEntry worker, bool isHost, bool isWorldReady,
        WorkerExperienceAction action)
    {
        if (!isHost || !isWorldReady || action == WorkerExperienceAction.None)
            return false;

        worker.Experience ??= new WorkerSkillExperience();
        int amount = action switch
        {
            WorkerExperienceAction.PickupForage => ForagingExperiencePerForage,
            WorkerExperienceAction.ChopTree => ForagingExperiencePerTree,
            WorkerExperienceAction.ChopHardwood => ForagingExperiencePerHardwood,
            WorkerExperienceAction.ClearWeeds or WorkerExperienceAction.ClearTwig => ExperiencePerSmallDebris,
            WorkerExperienceAction.ClearStone => ExperiencePerSmallDebris,
            _ => 0,
        };
        if (amount == 0)
            return false;
        if (action == WorkerExperienceAction.ClearStone)
            worker.Experience.Mining = AddExperience(worker.Experience.Mining, amount);
        else
            worker.Experience.Foraging = AddExperience(worker.Experience.Foraging, amount);
        return true;
    }

    private static int AddExperience(int current, int amount)
        => (int)Math.Min(int.MaxValue, (long)Math.Max(0, current) + amount);
}

internal enum WorkerExperienceAction
{
    None,
    PickupForage,
    ChopTree,
    ChopHardwood,
    ClearWeeds,
    ClearTwig,
    ClearStone,
}
