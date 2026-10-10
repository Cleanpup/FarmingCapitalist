using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal readonly record struct WorkerRuntimeSnapshot(
    WorkerTaskKind AssignedTask,
    string State,
    string Status,
    int CompletedToday,
    Point? TargetTile);
