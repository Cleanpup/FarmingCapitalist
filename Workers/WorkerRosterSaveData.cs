using System.Collections.Generic;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerRosterSaveData
{
    public WorkerRosterSaveData()
    {
    }

    public int SchemaVersion { get; set; }

    public int NextWorkerNumber { get; set; } = 1;

    public List<WorkerRosterEntry> Workers { get; set; } = new();
}
