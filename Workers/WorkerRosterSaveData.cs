using System.Collections.Generic;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerRosterSaveData
{
    public List<WorkerRosterEntry> Workers { get; set; } = new();
}
