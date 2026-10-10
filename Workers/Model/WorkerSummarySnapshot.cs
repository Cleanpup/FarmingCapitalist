using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal readonly record struct WorkerSummarySnapshot(
    string WorkerId,
    string DisplayName,
    WorkerProfession Profession,
    string ForageLocationName,
    string CombatArea,
    string ExplorationArea,
    string MiningArea,
    string FishingArea,
    bool IsConfigured,
    bool IsSpawned,
    string? CurrentLocationName,
    Point? CurrentTile);
