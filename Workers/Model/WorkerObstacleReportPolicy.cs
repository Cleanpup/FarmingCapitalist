using System;

namespace FarmingCapitalist.Workers;

internal static class WorkerObstacleReportPolicy
{
    public static bool MatchesAcknowledgment(WorkerObstacleReport? pending, string reportId)
        => pending is not null
            && !string.IsNullOrWhiteSpace(reportId)
            && string.Equals(pending.Id, reportId, StringComparison.Ordinal);
}
