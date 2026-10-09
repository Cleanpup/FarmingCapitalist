namespace FarmingCapitalist.Workers;

internal sealed class WorkerExplorationLoot
{
    public string Area { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int Stack { get; set; }
    public WorkerExplorationLoot Clone() => new() { Area = this.Area, ItemId = this.ItemId, Stack = this.Stack };
}

/// <summary>Only simulated progress and undelivered items are saved, never dungeon objects.</summary>
internal sealed class WorkerExplorationProgress
{
    public int Day { get; set; } = -1;
    public int Minutes { get; set; }
    public int LastObservedMinute { get; set; } = -1;
    public int CompletedRuns { get; set; }
    public List<WorkerExplorationLoot> PendingLoot { get; set; } = new();
    public WorkerExplorationProgress Clone() => new()
    {
        Day = this.Day, Minutes = this.Minutes, LastObservedMinute = this.LastObservedMinute,
        CompletedRuns = this.CompletedRuns,
        PendingLoot = (this.PendingLoot ?? new()).Where(item => item is not null).Select(item => item.Clone()).ToList(),
    };
}
