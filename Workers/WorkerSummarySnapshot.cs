using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal readonly record struct WorkerSummarySnapshot(
    string WorkerId,
    string DisplayName,
    bool IsConfigured,
    bool IsSpawned,
    string? CurrentLocationName,
    Point? CurrentTile);
