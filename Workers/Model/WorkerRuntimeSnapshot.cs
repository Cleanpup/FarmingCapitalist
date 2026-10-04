using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal readonly record struct WorkerRuntimeSnapshot(
    WorkerTaskKind AssignedTask,
    string State,
    string Status,
    int CompletedToday,
    Point? TargetTile);
