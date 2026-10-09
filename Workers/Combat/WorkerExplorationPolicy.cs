namespace FarmingCapitalist.Workers;

/// <summary>Simulation timing, access and area-specific loot; no live monster or farmer rewards.</summary>
internal static class WorkerExplorationPolicy
{
    public const int MinutesPerRun = 60;
    public const int MiningExperiencePerRun = 5;
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
            || !WorkerExperiencePolicy.TryAwardExploration(worker, isHost, worldReady))
            return false;
        progress.Minutes -= MinutesPerRun;
        progress.CompletedRuns++;
        progress.PendingLoot.AddRange(loot.Select(item => item.Clone()));
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

    public static List<WorkerExplorationLoot> RollLoot(string area, Random random, int deepestMineLevel)
    {
        if (!WorkerExplorationAreaCatalog.IsValid(area))
            throw new ArgumentOutOfRangeException(nameof(area));
        List<WorkerExplorationLoot> loot = new();
        void Add(string id, int count) => loot.Add(new() { ItemId = "(O)" + id, Stack = count });
        Add("390", random.Next(2, 5)); // stone, shared by all three areas
        switch (area)
        {
            case WorkerExplorationAreaCatalog.Mines:
                // Reflect the regular mine bands the player has unlocked, without
                // creating those floors or changing the player's deepest level.
                int band = deepestMineLevel >= 80 ? random.Next(3)
                    : deepestMineLevel >= 40 ? random.Next(2) : 0;
                Add(band switch { 1 => "380", 2 => "384", _ => "378" }, random.Next(1, 3));
                if (random.NextDouble() < 0.4)
                    Add(band switch { 1 => "767", 2 => "769", _ => random.Next(2) == 0 ? "684" : "766" }, 1);
                if (random.NextDouble() < 0.1)
                    Add(band switch { 1 => "536", 2 => "537", _ => "535" }, 1);
                break;
            case WorkerExplorationAreaCatalog.SkullCavern:
                Add("384", random.Next(1, 3)); // gold
                if (random.NextDouble() < 0.1) Add("386", 1); // uncommon iridium
                if (random.NextDouble() < 0.4) Add(random.Next(2) == 0 ? "768" : "769", 1);
                if (random.NextDouble() < 0.1) Add("749", 1); // omni geode
                break;
            case WorkerExplorationAreaCatalog.Volcano:
                Add("848", random.Next(1, 3)); // cinder shards
                if (random.NextDouble() < 0.25) Add("384", 1); // gold-bearing volcanic rock
                if (random.NextDouble() < 0.4) Add("768", 1); // magma sprite solar essence
                if (random.NextDouble() < 0.1) Add("537", 1); // magma geode
                break;
        }
        if (random.NextDouble() < 0.3) Add("382", 1); // coal
        return loot;
    }
}
