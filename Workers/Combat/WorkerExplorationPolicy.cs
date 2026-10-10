namespace HireSkilledHelpers.Workers;

/// <summary>Simulation timing, access and area-specific loot; no live monster or farmer rewards.</summary>
internal static class WorkerExplorationPolicy
{
    public const int MinutesPerRun = 60;
    public const int CombatExperiencePerRun = 5;
    public const int MaximumRunsPerDay = 16;
    public static int WorkMinute(int time) => Math.Clamp(time / 100 * 60 + time % 100, 360, 1320);
    public static int ElapsedMinutes(int previous, int current)
        => previous < 0 ? 0 : Math.Max(0, current - previous);

    public static bool ObserveClock(WorkerExplorationProgress progress, int day, int time, bool accrue)
    {
        bool changed = false;
        if (progress.Day != day)
        {
            progress.Day = day;
            progress.Minutes = progress.CompletedRuns = 0;
            progress.LastObservedMinute = -1;
            changed = true;
        }
        int minute = WorkMinute(time);
        // Rewinding the clock must not make the same interval pay out twice.
        if (minute <= progress.LastObservedMinute) return changed;
        if (accrue) progress.Minutes += ElapsedMinutes(progress.LastObservedMinute, minute);
        progress.LastObservedMinute = minute;
        return true;
    }

    public static bool TryQueueRun(WorkerRosterEntry worker, WorkerExplorationProgress progress,
        List<WorkerExplorationLoot> loot, int day, bool isHost, bool worldReady, bool paid, bool defeated)
    {
        if (!isHost || !worldReady || !paid || defeated || worker.AssignedTask != WorkerTaskKind.ExploreArea
            || progress.Day != day || progress.Minutes < MinutesPerRun || progress.CompletedRuns >= MaximumRunsPerDay
            || worker.Exploration.Day != day || worker.Exploration.CompletedRuns != progress.CompletedRuns
            || worker.Exploration.Minutes != progress.Minutes
            || loot.Count == 0 || loot.Any(item => item.Stack <= 0 || !IsAllowedMonsterLoot(worker.ExplorationArea, item.ItemId))
            || !WorkerExperiencePolicy.TryAwardExploration(worker, isHost, worldReady))
            return false;
        progress.Minutes -= MinutesPerRun;
        progress.CompletedRuns++;
        progress.PendingLoot.AddRange(loot.Select(item =>
        {
            WorkerExplorationLoot copy = item.Clone();
            copy.Area = worker.ExplorationArea;
            return copy;
        }));
        worker.Exploration = progress.Clone();
        return true;
    }

    public static string? UnavailableReason(string area, bool minesOpen, bool busRepaired,
        bool skullKey, bool boatRepaired, bool islandNorthOpen) => area switch
    {
        WorkerExplorationAreaCatalog.Mines when !minesOpen => "The mine entrance is still blocked",
        WorkerExplorationAreaCatalog.SkullCavern when !busRepaired => "Repair the bus before exploring Skull Cavern",
        WorkerExplorationAreaCatalog.SkullCavern when !skullKey => "Find the Skull Key before exploring Skull Cavern",
        WorkerExplorationAreaCatalog.Volcano when !boatRepaired => "Repair Willy's boat before exploring the Volcano",
        WorkerExplorationAreaCatalog.Volcano when !islandNorthOpen => "Open the north path on Ginger Island first",
        _ when !WorkerExplorationAreaCatalog.IsValid(area) => "Unknown exploration area",
        _ => null,
    };

    public static bool IsAllowedMonsterLoot(string area, string itemId)
        => WorkerExplorationLootCatalog.IsAllowed(area, itemId);

    public static bool IsAllowedPendingLoot(WorkerExplorationLoot item)
        => item.Stack > 0 && WorkerExplorationAreaCatalog.IsValid(item.Area)
            && IsAllowedMonsterLoot(item.Area, item.ItemId);

    public static void TagLegacyPendingLoot(WorkerExplorationProgress progress, string savedArea)
    {
        // 1.3.0 did not record a source on queued items. Use its saved destination,
        // never a union of all areas that could admit an unrelated reward.
        foreach (WorkerExplorationLoot item in progress.PendingLoot)
            if (string.IsNullOrEmpty(item.Area)) item.Area = savedArea;
    }

    public static List<WorkerExplorationLoot> RollLoot(string area, Random random, int deepestMineLevel,
        bool slimeHutchBuilt = false)
        => WorkerExplorationLootCatalog.Roll(area, random, deepestMineLevel, slimeHutchBuilt);
}
